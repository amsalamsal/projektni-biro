# frozen_string_literal: true

require_relative 'test_helper'

class TestChangePlanner < Minitest::Test
  include SdpTest

  PROJECT = '11111111-1111-4111-8111-111111111111'

  def setup
    @ids = Array.new(6) { SecureRandom.uuid }
    specs = @ids.first(5).each_with_index.map { |id, i| { type: 'furniture.bench', id: id, hash: (i.to_s * 64)[0, 64] } }
    @pkg, = package(specs, project_id: PROJECT)
  end

  def entry(id, hash, fingerprint_changed: false, project: PROJECT, deleted: false, local: nil)
    ModelEntry.new(id: id, project_id: project, type_id: 'furniture.bench', content_hash: hash,
                   stored_fingerprint: 'f1', current_fingerprint: fingerprint_changed ? 'f2' : 'f1',
                   deleted_in_source: deleted, local_params: local, ref: id)
  end

  def kinds(plan)
    plan.actions.map { |a| [a.kind, a.element&.id || a.entry&.id] }.to_h { |k, id| [id, k] }
  end

  def test_first_import_creates_everything
    plan = ChangePlanner.new.plan(@pkg, [])
    assert_equal 5, plan.of(:create).length
    assert plan.changes?
  end

  def test_repeated_import_without_changes_creates_nothing
    entries = @pkg.elements.map { |e| entry(e.id, e.content_hash) }
    plan = ChangePlanner.new.plan(@pkg, entries)
    assert_equal 5, plan.of(:unchanged).length
    refute plan.changes?
  end

  def test_update_conflict_and_local_edits
    e = @pkg.elements
    entries = [
      entry(e[0].id, e[0].content_hash),                                # unchanged
      entry(e[1].id, 'old'),                                            # changed in AutoCAD -> update
      entry(e[2].id, 'old', fingerprint_changed: true),                 # changed in both -> conflict
      entry(e[3].id, e[3].content_hash, fingerprint_changed: true),     # edited in SketchUp only -> keep
      entry(e[4].id, e[4].content_hash, local: { 'length' => 2.2 })    # parameter edit in SketchUp -> keep
    ]
    k = kinds(ChangePlanner.new.plan(@pkg, entries))
    assert_equal :unchanged, k[e[0].id]
    assert_equal :update, k[e[1].id]
    assert_equal :conflict, k[e[2].id]
    assert_equal :keep_local, k[e[3].id]
    assert_equal :keep_local, k[e[4].id]

    k = kinds(ChangePlanner.new(modified: 'overwrite').plan(@pkg, entries))
    assert_equal :update, k[e[2].id]
  end

  def test_deleted_elements_follow_policy_and_ignore_other_projects
    gone = entry(@ids[5], 'x')
    other = entry(SecureRandom.uuid, 'y', project: '22222222-2222-4222-8222-222222222222')
    base = @pkg.elements.map { |e| entry(e.id, e.content_hash) }
    assert_equal :mark_deleted, kinds(ChangePlanner.new.plan(@pkg, base + [gone, other]))[@ids[5]]
    assert_equal :delete, kinds(ChangePlanner.new(deleted: 'delete').plan(@pkg, base + [gone]))[@ids[5]]
    assert_equal :keep_orphan, kinds(ChangePlanner.new(deleted: 'keep').plan(@pkg, base + [gone]))[@ids[5]]
    refute kinds(ChangePlanner.new.plan(@pkg, base + [other])).key?(other.id), 'other projects are never touched'
    already = entry(@ids[5], 'x', deleted: true)
    refute ChangePlanner.new.plan(@pkg, base + [already]).changes?
  end

  def test_restore_and_duplicates
    e = @pkg.elements
    entries = [entry(e[0].id, e[0].content_hash, deleted: true),
               entry(e[1].id, e[1].content_hash), entry(e[1].id, e[1].content_hash, fingerprint_changed: true)]
    plan = ChangePlanner.new.plan(@pkg, entries)
    assert_equal :restore, kinds(plan)[e[0].id]
    dup = plan.of(:detach_duplicate)
    assert_equal 1, dup.length
    assert_equal 'f2', dup.first.entry.current_fingerprint, 'the modified copy is detached, the original stays managed'
    assert_equal 1, plan.of(:unchanged).count { |a| a.element&.id == e[1].id }
  end

  def test_unsupported_elements_are_skipped
    plan = ChangePlanner.new.plan(@pkg, [], ->(el) { el.id != @pkg.elements[0].id })
    assert_equal 1, plan.of(:skip).length
  end

  def test_invalid_policy_rejected_and_summary
    assert_raises(ArgumentError) { ChangePlanner.new(modified: 'merge') }
    plan = ChangePlanner.new.plan(@pkg, [])
    assert_equal 'New: 5', plan.summary
    assert_equal 5, plan.to_h['actions'].length
  end
end
