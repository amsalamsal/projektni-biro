# frozen_string_literal: true

require_relative 'test_helper'

# Automatic updates: the description of the newest version (latest.json) and the comparison of versions, read the
# same way as in AutoCAD (UpdateTests.cs).
class TestUpdates < Minitest::Test
  include SdpTest
  U = Updates
  SHA = '0123456789abcdef' * 4

  def json(version, sha = SHA)
    JSON.generate('version' => version, 'date' => '2026-11-23', 'notes' => ['Ispravka A', ''],
                  'autocad' => { 'url' => 'https://example.com/a.zip', 'sha256' => sha },
                  'sketchup' => { 'url' => 'https://example.com/pb_site_design_pro.rbz', 'sha256' => sha.upcase })
  end

  def test_the_description_of_a_version_is_read
    info = U.parse(json('0.3.2'))
    assert_equal '0.3.2', info['version']
    assert_equal ['Ispravka A'], info['notes']
    assert_equal SHA, info['sha256']
    assert info['url'].end_with?('.rbz')
  end

  def test_an_incomplete_description_is_refused
    assert_raises(ArgumentError) { U.parse('{"version":"0.3.2"}') }
    assert_raises(ArgumentError) { U.parse(json('0.3.2', 'abc')) }
    assert_raises(ArgumentError) { U.parse('<html>nije json</html>') }
  end

  def test_versions_are_compared_by_number
    assert U.newer?('0.3.2', '0.3.1')
    assert U.newer?('0.10.0', '0.9.9')
    assert U.newer?('v1.0', '0.9.9')
    refute U.newer?('0.3.1', '0.3.1')
    refute U.newer?('0.3.0', '0.3.1')
    refute U.newer?('x', '0.3.1')
  end
end
