# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Roads
      # Drawing tools of the road commands. Points are picked with the SketchUp inference (on the terrain, on edges,
      # along the axes); everything given to the callbacks is in metres [x, y, z] (model frame).
      module Tools
        PREVIEW_COLORS = {
          axis: [30, 30, 30], carriageway: [90, 90, 90], kerb: [140, 140, 140], bike: [128, 0, 32], sidewalk: [70, 70, 70],
          edge: [150, 150, 150], helper: [0, 120, 215], margin: [230, 180, 0], warning: [220, 40, 40]
        }.freeze

        module_function

        def to_m(p)
          [Units.in_to_m(p.x.to_f), Units.in_to_m(p.y.to_f), Units.in_to_m(p.z.to_f)]
        end

        def to_in(v)
          Geom::Point3d.new(Units.m_to_in(v[0].to_f), Units.m_to_in(v[1].to_f), Units.m_to_in((v[2] || 0).to_f))
        end

        # Preview lines: [[points_m, :role], ...]
        def draw_lines(view, lines)
          lines.each do |pts, role|
            next if pts.nil? || pts.length < 2

            c = PREVIEW_COLORS[role] || PREVIEW_COLORS[:axis]
            view.drawing_color = Sketchup::Color.new(*c)
            view.line_width = role == :axis || role == :helper ? 2 : 1
            view.line_stipple = role == :helper ? '-' : ''
            view.draw(GL_LINE_STRIP, pts.map { |v| to_in(v) })
          end
          view.line_stipple = ''
        end

        def length_text(text)
          Roads.parse_number(text)
        end

        # Base of the point tools: the input point, the status text and the VCB.
        class BaseTool
          def initialize(prompt)
            @prompt = prompt
            @ip = Sketchup::InputPoint.new
            @mouse = nil
          end

          def activate
            status
          end

          def deactivate(view)
            view.invalidate
          end

          def resume(view)
            status
            view.invalidate
          end

          def status
            Sketchup.status_text = @prompt.to_s
          end

          def pick(view, x, y, from)
            from ? @ip.pick(view, x, y, from) : @ip.pick(view, x, y)
          end

          def onMouseMove(_flags, x, y, view)
            pick(view, x, y, @from_ip)
            @mouse = Tools.to_m(@ip.position)
            view.tooltip = @ip.tooltip if @ip.valid?
            view.invalidate
          end

          def draw(view)
            @ip.draw(view) if @ip.valid? && @ip.display?
          end

          def onCancel(_reason, view)
            cancel(view)
          end

          def cancel(view)
            Sketchup.active_model.select_tool(nil)
            view.invalidate
          end
        end

        # A polyline point by point (axes of streets and roads, centreline of a combined ramp). Enter or a double
        # click ends it, Backspace removes the last point, Esc cancels. The VCB takes the length of the next segment
        # (in the direction of the cursor), "length;height" or "v height" (the height of the next point).
        class PolylineTool < BaseTool
          # heights: :click (Z of the picked point), :same (one height), :each (asked after every point)
          def initialize(prompt, heights: :click, height: 0.0, base: nil, min_points: 2, closable: true, preview: nil, snap: nil, &finish)
            super(prompt)
            @heights = heights
            @height = height
            @base = base
            @min_points = min_points
            @closable = closable
            @preview = preview
            @snap = snap
            @finish = finish
            @points = []
            @next_height = nil
            @from_ip = nil
          end

          def activate
            Sketchup.vcb_label = 'Dužina; visina' if Sketchup.respond_to?(:vcb_label=)
            status
          end

          def status
            Sketchup.status_text = "#{@prompt}  Tačaka: #{@points.length}. Enter ili dvostruki klik = kraj, Backspace = poništi tačku, " \
                                   "Esc = odustani. VCB: dužina u m, „dužina;visina” ili „v visina”."
          end

          def onMouseMove(flags, x, y, view)
            @from_ip = @points.empty? ? nil : @last_ip
            super
            @mouse = @snap.call(@points, @mouse) if @snap && @mouse
          end

          def current_point
            return nil unless @mouse

            p = @mouse.dup
            p[2] = height_for(p)
            p
          end

          def height_for(p)
            return @next_height if @next_height
            return @height if @heights == :same
            return @points.last[2] if @heights == :each && !@points.empty?

            @heights == :each ? @height : p[2]
          end

          def onLButtonDown(_flags, x, y, view)
            pick(view, x, y, @points.empty? ? nil : @last_ip)
            return unless @ip.valid?

            @mouse = Tools.to_m(@ip.position)
            @mouse = @snap.call(@points, @mouse) if @snap
            add_point(current_point, view)
            @last_ip = Sketchup::InputPoint.new(@ip.position)
          end

          def add_point(p, view)
            return if p.nil?
            return finish(view) if !@points.empty? && Geometry.dist(p, @points.last) < 1e-4 && @points.length >= @min_points

            if @closable && @points.length >= 3 && Geometry.dist(p, @points.first) < 0.05
              @closed = true
              return finish(view)
            end

            if @heights == :each && !@points.empty? && @next_height.nil?
              h = ask_height(@points.last[2])
              return if h.nil?

              p[2] = h
            end
            warn_slope(p)
            @points << p
            @next_height = nil
            status
            view.invalidate
          end

          def warn_slope(p)
            return if @points.empty?

            run = Geometry.dist(p, @points.last)
            slope = run > 1e-6 ? (p[2] - @points.last[2]).abs / run : 0
            Sketchup.status_text = "UPOZORENJE: uzdužni nagib do ove tačke je #{Roads.num(slope * 100)} % (dozvoljeno najviše 10 %)." if slope > MAX_ROAD_SLOPE + 1e-9
          end

          def ask_height(last)
            base = @base ? " (Z = 0 je #{Roads.num(@base)} m n.v.; može se upisati i m n.v.)" : ''
            r = UI.inputbox(["Visina tačke (m)#{base}"], [Roads.num(last)], 'Visina tačke osovine')
            return nil unless r

            v = Roads.parse_number(r[0])
            v.nil? ? last : Roads.to_relative(v, @base)
          end

          def onLButtonDoubleClick(_flags, _x, _y, view)
            finish(view)
          end

          def onReturn(view)
            finish(view)
          end

          def onKeyDown(key, _repeat, _flags, view)
            return unless key == 8 # Backspace

            @points.pop
            status
            view.invalidate
          end

          def onUserText(text, view)
            t = text.to_s.strip
            if t =~ /\Av\s*=?\s*(.+)\z/i
              v = Roads.parse_number(Regexp.last_match(1))
              return UI.beep unless v

              @next_height = Roads.to_relative(v, @base)
              Sketchup.status_text = "Visina sljedeće tačke: #{Roads.num(@next_height)} m"
              return
            end
            len_text, h_text = t.split(';', 2)
            # a plain number is metres (as all road inputs); with a unit (20m, 2500cm, 30') SketchUp converts it
            len = Roads.parse_number(len_text)
            if len.nil? && len_text.to_s.strip.respond_to?(:to_l)
              len = begin
                len_text.to_s.strip.to_l.to_m
              rescue ArgumentError
                nil
              end
            end
            return UI.beep if len.nil? || @points.empty? || @mouse.nil?

            dir = Geometry.norm(Geometry.sub(@mouse, @points.last))
            return UI.beep if Geometry.len(dir) < 1e-9

            q = Geometry.add(@points.last, Geometry.scale(dir, len))
            h = h_text ? Roads.parse_number(h_text) : nil
            @next_height = Roads.to_relative(h, @base) if h
            p = [q[0], q[1], @next_height || height_for([q[0], q[1], @points.last[2]])]
            add_point(p, view)
          end

          def finish(view)
            pts = @points
            closed = @closed == true
            if pts.length < @min_points
              UI.messagebox("Potrebne su najmanje #{@min_points} tačke.") unless pts.empty?
              return cancel(view)
            end
            Sketchup.active_model.select_tool(nil)
            @finish.call(pts, closed)
          end

          def draw(view)
            super
            pts = @points.dup
            cur = current_point
            pts << cur if cur
            lines = [[pts, :axis]]
            lines.concat(@preview.call(pts) || []) if @preview && pts.length >= 2
            Tools.draw_lines(view, lines)
          end

          def getExtents
            bb = Geom::BoundingBox.new
            @points.each { |p| bb.add(Tools.to_in(p)) }
            bb.add(Tools.to_in(@mouse)) if @mouse
            bb
          end
        end

        # One or more single points (crossings, marks, ramps, centres). The block gets the point (metres) and returns
        # :continue to pick another point, :done to end the tool, or anything else when it has selected the next
        # tool itself. Enter ends it too.
        class PickTool < BaseTool
          def initialize(prompt, from: nil, preview: nil, snap: nil, &pick)
            super(prompt)
            @from = from
            @preview = preview
            @snap = snap
            @pick = pick
            @from_ip = from ? Sketchup::InputPoint.new(Tools.to_in(from)) : nil
          end

          attr_writer :prompt

          def onMouseMove(flags, x, y, view)
            super
            @mouse = @snap.call(@mouse) if @snap && @mouse
          end

          def onLButtonDown(_flags, x, y, view)
            pick(view, x, y, @from_ip)
            return unless @ip.valid?

            p = Tools.to_m(@ip.position)
            p = @snap.call(p) if @snap
            case @pick.call(p, view)
            when :continue
              status
              view.invalidate
            when :done then Sketchup.active_model.select_tool(nil)
            end # anything else: the block has selected the next tool
          end

          def onReturn(view)
            cancel(view)
          end

          def draw(view)
            super
            lines = []
            lines << [[@from, @mouse], :axis] if @from && @mouse
            lines.concat(@preview.call(@mouse) || []) if @preview && @mouse
            Tools.draw_lines(view, lines)
          end
        end
      end
    end
  end
end
