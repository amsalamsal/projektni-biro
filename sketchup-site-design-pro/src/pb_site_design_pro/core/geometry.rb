# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Plan-geometry helpers on plain arrays ([x, y] or [x, y, z], metres). Mirrors the C# GeometryMath
    # conventions: bulge = tan(sweep / 4), positive = counter-clockwise; bulge i applies to segment i -> i+1.
    module Geometry
      EPS = 1e-9

      module_function

      def sub(a, b)
        [a[0] - b[0], a[1] - b[1]]
      end

      def add(a, b)
        [a[0] + b[0], a[1] + b[1]]
      end

      def scale(a, s)
        [a[0] * s, a[1] * s]
      end

      def dot(a, b)
        (a[0] * b[0]) + (a[1] * b[1])
      end

      def cross(a, b)
        (a[0] * b[1]) - (a[1] * b[0])
      end

      def len(a)
        Math.sqrt((a[0] * a[0]) + (a[1] * a[1]))
      end

      def dist(a, b)
        len(sub(a, b))
      end

      def norm(a)
        l = len(a)
        l < 1e-15 ? [0.0, 0.0] : [a[0] / l, a[1] / l]
      end

      # Left perpendicular (rotated +90 degrees).
      def perp(a)
        [-a[1], a[0]]
      end

      def rotate(a, angle)
        c = Math.cos(angle)
        s = Math.sin(angle)
        [(a[0] * c) - (a[1] * s), (a[0] * s) + (a[1] * c)]
      end

      def lerp3(a, b, t)
        [a[0] + ((b[0] - a[0]) * t), a[1] + ((b[1] - a[1]) * t), (a[2] || 0) + (((b[2] || 0) - (a[2] || 0)) * t)]
      end

      # Arc data for a bulged segment: { center:, radius:, start:, sweep: }.
      def arc_from_bulge(a, b, bulge)
        chord = sub(b, a)
        c = len(chord)
        return { center: a[0, 2], radius: 0.0, start: 0.0, sweep: 0.0 } if c < EPS

        sweep = 4.0 * Math.atan(bulge)
        radius = c * (1 + (bulge * bulge)) / (4 * bulge.abs)
        mid = scale(add(a, b), 0.5)
        n = perp(norm(chord))
        center = add(mid, scale(n, c / 2.0 * (1 - (bulge * bulge)) / (2 * bulge)))
        start = Math.atan2(a[1] - center[1], a[0] - center[0])
        { center: center, radius: radius, start: start, sweep: sweep }
      end

      def segment_length(a, b, bulge)
        return dist(a, b) if bulge.nil? || bulge.zero?

        arc = arc_from_bulge(a, b, bulge)
        arc[:radius] * arc[:sweep].abs
      end

      # Point [x, y, z] and tangent angle at parameter t (0..1) of a segment.
      def segment_point(a, b, bulge, t)
        z = (a[2] || 0) + (((b[2] || 0) - (a[2] || 0)) * t)
        if bulge.nil? || bulge.zero?
          d = sub(b, a)
          return [[a[0] + (d[0] * t), a[1] + (d[1] * t), z], Math.atan2(d[1], d[0])]
        end

        arc = arc_from_bulge(a, b, bulge)
        ang = arc[:start] + (arc[:sweep] * t)
        pt = [arc[:center][0] + (Math.cos(ang) * arc[:radius]), arc[:center][1] + (Math.sin(ang) * arc[:radius]), z]
        tangent = ang + ((arc[:sweep].positive? ? 1 : -1) * Math::PI / 2)
        [pt, tangent]
      end

      def segment_count(vertices, closed)
        closed ? vertices.length : [vertices.length - 1, 0].max
      end

      # Straight-segment approximation. Open paths include the last vertex; closed rings do not repeat the first.
      def tessellate(vertices, bulges, closed, max_dev = 0.02, max_steps = 128)
        out = []
        n = segment_count(vertices, closed)
        n.times do |i|
          a = vertices[i]
          b = vertices[(i + 1) % vertices.length]
          out << a.dup
          bulge = bulges ? (bulges[i] || 0.0) : 0.0
          next if bulge.zero?

          arc = arc_from_bulge(a, b, bulge)
          next if arc[:radius] < EPS

          steps = arc_steps(arc, max_dev, max_steps)
          (1...steps).each { |s| out << segment_point(a, b, bulge, s.to_f / steps)[0] }
        end
        out << vertices.last.dup if !closed && !vertices.empty?
        out
      end

      def arc_steps(arc, max_dev, max_steps)
        tol = [[max_dev, arc[:radius]].min, 1e-6].max
        step = 2 * Math.acos([[1 - (tol / arc[:radius]), -1.0].max, 1.0].min)
        return max_steps if step < 1e-6

        [[(arc[:sweep].abs / step).ceil, 2].max, max_steps].min
      end

      def polyline_length(points, closed = false)
        total = 0.0
        n = segment_count(points, closed)
        n.times { |i| total += dist(points[i], points[(i + 1) % points.length]) }
        total
      end

      def signed_area(points)
        a = 0.0
        points.each_with_index do |p, i|
          q = points[(i + 1) % points.length]
          a += (p[0] * q[1]) - (q[0] * p[1])
        end
        a / 2.0
      end

      def ccw(points)
        signed_area(points).negative? ? points.reverse : points
      end

      def point_in_polygon?(p, poly)
        inside = false
        j = poly.length - 1
        poly.each_with_index do |a, i|
          b = poly[j]
          if (a[1] > p[1]) != (b[1] > p[1]) &&
             p[0] < ((b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1])) + a[0]
            inside = !inside
          end
          j = i
        end
        inside
      end

      # Removes consecutive duplicates (and a closing duplicate for closed rings).
      def dedupe(points, closed, tol = 1e-6)
        out = []
        points.each { |p| out << p if out.empty? || dist(out.last, p) > tol }
        out.pop if closed && out.length > 1 && dist(out.first, out.last) <= tol
        out
      end

      # Removes vertices on straight lines (collinear within tolerance) from a closed ring.
      def simplify_collinear(points, tol = 1e-7)
        return points if points.length <= 3

        out = []
        n = points.length
        points.each_with_index do |p, i|
          prev = points[(i - 1) % n]
          nxt = points[(i + 1) % n]
          c = cross(norm(sub(p, prev)), norm(sub(nxt, p)))
          d = dot(norm(sub(p, prev)), norm(sub(nxt, p)))
          out << p unless c.abs < tol && d.positive?
        end
        out.length >= 3 ? out : points
      end

      # Offsets an open polyline: returns [left_line, right_line] where left is offset by +d_left
      # (to the left of travel) and right by -d_right. Mitred joins, bevelled beyond the miter limit.
      def offset_open(points, d_left, d_right, miter_limit = 4.0)
        [offset_line(points, d_left, miter_limit), offset_line(points, -d_right, miter_limit)]
      end

      def offset_line(points, d, miter_limit = 4.0)
        n = points.length
        return points.map(&:dup) if n < 2 || d.zero?

        out = []
        n.times do |i|
          z = points[i][2] || 0.0
          if i.zero? || i == n - 1
            seg = i.zero? ? norm(sub(points[1], points[0])) : norm(sub(points[n - 1], points[n - 2]))
            off = scale(perp(seg), d)
            out << [points[i][0] + off[0], points[i][1] + off[1], z]
            next
          end

          d1 = norm(sub(points[i], points[i - 1]))
          d2 = norm(sub(points[i + 1], points[i]))
          n1 = perp(d1)
          n2 = perp(d2)
          bis = norm(add(n1, n2))
          cos_half = dot(bis, n1)
          if cos_half.abs < 1e-9 || (1.0 / cos_half.abs) > miter_limit
            # bevel: two points
            a = scale(n1, d)
            b = scale(n2, d)
            out << [points[i][0] + a[0], points[i][1] + a[1], z]
            out << [points[i][0] + b[0], points[i][1] + b[1], z]
          else
            m = scale(bis, d / cos_half)
            out << [points[i][0] + m[0], points[i][1] + m[1], z]
          end
        end
        out
      end

      # Strip polygon (counter-clockwise) around an open polyline: width to the left/right of travel.
      def buffer_open(points, d_left, d_right)
        left, right = offset_open(points, d_left, d_right)
        ccw(left + right.reverse)
      end

      # Mitred offset of a closed counter-clockwise ring: positive d grows the ring, negative shrinks it.
      # Returns nil when the offset collapses or inverts the ring.
      def offset_ring(points, d, miter_limit = 6.0)
        ring = ccw(simplify_collinear(dedupe(points, true)))
        n = ring.length
        return nil if n < 3

        out = ring.each_with_index.map do |p, i|
          prev = ring[(i - 1) % n]
          nxt = ring[(i + 1) % n]
          # outward normal for a CCW ring is the right-hand perpendicular of the edge direction
          n1 = scale(perp(norm(sub(p, prev))), -1)
          n2 = scale(perp(norm(sub(nxt, p))), -1)
          bis = norm(add(n1, n2))
          cos_half = dot(bis, n1)
          k = cos_half.abs < 1e-9 ? miter_limit : [1.0 / cos_half, miter_limit].min
          m = scale(bis, d * k)
          [p[0] + m[0], p[1] + m[1], p[2] || 0.0]
        end
        a0 = signed_area(ring)
        a1 = signed_area(out)
        return nil if a1 <= 1e-9 || (d.negative? && a1 >= a0)
        # every offset edge must keep the direction of its source edge; reversed edges mean the offset
        # passed through itself (e.g. insetting a square by more than half its width)
        n.times do |i|
          j = (i + 1) % n
          return nil unless dot(sub(out[j], out[i]), sub(ring[j], ring[i])).positive?
        end
        return nil if self_intersecting?(out)

        out
      end

      def segments_cross?(a, b, c, d)
        d1 = cross(sub(b, a), sub(c, a))
        d2 = cross(sub(b, a), sub(d, a))
        d3 = cross(sub(d, c), sub(a, c))
        d4 = cross(sub(d, c), sub(b, c))
        ((d1.positive? && d2.negative?) || (d1.negative? && d2.positive?)) &&
          ((d3.positive? && d4.negative?) || (d3.negative? && d4.positive?))
      end

      def self_intersecting?(ring)
        n = ring.length
        return false if n < 4

        n.times do |i|
          a = ring[i]
          b = ring[(i + 1) % n]
          ((i + 2)...n).each do |j|
            next if i.zero? && j == n - 1
            return true if segments_cross?(a, b, ring[j], ring[(j + 1) % n])
          end
        end
        false
      end

      # Oriented rectangle aligned with the first boundary edge; :v points into the area.
      # Returns { origin: [x, y], u:, v:, length_u:, length_v:, z: }.
      def oriented_rect(points)
        o = points[0]
        u = norm(sub(points[1], points[0]))
        u = [1.0, 0.0] if len(u) < 0.5
        v = perp(u)
        us = points.map { |p| dot(sub(p, o), u) }
        vs = points.map { |p| dot(sub(p, o), v) }
        min_v = vs.min
        max_v = vs.max
        if min_v.abs > max_v.abs
          v = scale(v, -1)
          min_v, max_v = -max_v, -min_v
        end
        origin = add(add(o[0, 2], scale(u, us.min)), scale(v, min_v))
        { origin: origin, u: u, v: v, length_u: us.max - us.min, length_v: max_v - min_v, z: o[2] || 0.0 }
      end

      def rect_point(rect, a, b, z = nil)
        p = add(add(rect[:origin], scale(rect[:u], a)), scale(rect[:v], b))
        [p[0], p[1], z.nil? ? rect[:z] : z]
      end

      def rectangle?(points, tol_deg = 0.5)
        return false unless points.length == 4

        tol = Math.sin(tol_deg * Math::PI / 180)
        4.times.all? do |i|
          a = norm(sub(points[(i + 1) % 4], points[i]))
          b = norm(sub(points[(i + 2) % 4], points[(i + 1) % 4]))
          dot(a, b).abs <= tol
        end
      end

      # Distributes stations along an open path given by vertices + bulges so that the distance
      # between consecutive stations never exceeds max_spacing. Stations are placed at every original
      # vertex (corners) and evenly inside each run. Returns [[x, y, z], ...].
      def stations(vertices, bulges, closed, max_spacing)
        n = segment_count(vertices, closed)
        pts = []
        n.times do |i|
          a = vertices[i]
          b = vertices[(i + 1) % vertices.length]
          bulge = bulges ? (bulges[i] || 0.0) : 0.0
          l = segment_length(a, b, bulge)
          next if l < 1e-9

          k = [(l / max_spacing - 1e-9).ceil, 1].max
          pts << segment_point(a, b, bulge, 0.0)[0] if pts.empty?
          (1..k).each { |s| pts << segment_point(a, b, bulge, s.to_f / k)[0] }
        end
        pts.pop if closed && pts.length > 1 && dist(pts.first, pts.last) < 1e-6
        pts
      end
    end
  end
end
