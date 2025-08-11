using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Netwatch.Controls
{
    // A lightweight sparkline control optimized for time-based sliding windows.
    // Features:
    // - Values + Timestamps (Unix ms) mapped into a WindowSeconds sliding window
    // - Optional y-axis area with right-aligned labels
    // - Padding to avoid label/line clipping (top/bottom)
    // - ForceZeroBaseline to pin axis min to 0 for positive data
    // - Optional Catmull–Rom spline smoothing
    public class SparklineControl : FrameworkElement
    {
        public SparklineControl()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            CompositionTarget.Rendering += OnCompositionTargetRendering;
        }

        #region Dependency Properties

        public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
            nameof(Values), typeof(IReadOnlyList<double>), typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public IReadOnlyList<double> Values
        {
            get => (IReadOnlyList<double>)GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public static readonly DependencyProperty TimestampsProperty = DependencyProperty.Register(
            nameof(Timestamps), typeof(IReadOnlyList<long>), typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        // Unix time in milliseconds corresponding to Values entries
        public IReadOnlyList<long> Timestamps
        {
            get => (IReadOnlyList<long>)GetValue(TimestampsProperty);
            set => SetValue(TimestampsProperty, value);
        }

        public static readonly DependencyProperty WindowSecondsProperty = DependencyProperty.Register(
            nameof(WindowSeconds), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(60.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double WindowSeconds
        {
            get => (double)GetValue(WindowSecondsProperty);
            set => SetValue(WindowSecondsProperty, value);
        }

        public static readonly DependencyProperty ShowYAxisProperty = DependencyProperty.Register(
            nameof(ShowYAxis), typeof(bool), typeof(SparklineControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowYAxis
        {
            get => (bool)GetValue(ShowYAxisProperty);
            set => SetValue(ShowYAxisProperty, value);
        }

        public static readonly DependencyProperty YAxisWidthProperty = DependencyProperty.Register(
            nameof(YAxisWidth), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double YAxisWidth
        {
            get => (double)GetValue(YAxisWidthProperty);
            set => SetValue(YAxisWidthProperty, value);
        }

        public static readonly DependencyProperty PlotTopPaddingProperty = DependencyProperty.Register(
            nameof(PlotTopPadding), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(17.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double PlotTopPadding
        {
            get => (double)GetValue(PlotTopPaddingProperty);
            set => SetValue(PlotTopPaddingProperty, value);
        }

        public static readonly DependencyProperty PlotBottomPaddingProperty = DependencyProperty.Register(
            nameof(PlotBottomPadding), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double PlotBottomPadding
        {
            get => (double)GetValue(PlotBottomPaddingProperty);
            set => SetValue(PlotBottomPaddingProperty, value);
        }

        public static readonly DependencyProperty ForceZeroBaselineProperty = DependencyProperty.Register(
            nameof(ForceZeroBaseline), typeof(bool), typeof(SparklineControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ForceZeroBaseline
        {
            get => (bool)GetValue(ForceZeroBaselineProperty);
            set => SetValue(ForceZeroBaselineProperty, value);
        }

        public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
            nameof(LineBrush), typeof(System.Windows.Media.Brush), typeof(SparklineControl),
            new FrameworkPropertyMetadata(System.Windows.Media.Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public System.Windows.Media.Brush LineBrush
        {
            get => (System.Windows.Media.Brush)GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        // Back-compat with XAML using 'Stroke' instead of 'LineBrush'
        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            nameof(Stroke), typeof(System.Windows.Media.Brush), typeof(SparklineControl),
            new FrameworkPropertyMetadata(System.Windows.Media.Brushes.DeepSkyBlue,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, e) => ((SparklineControl)d).LineBrush = (System.Windows.Media.Brush)e.NewValue));

        public System.Windows.Media.Brush Stroke
        {
            get => (System.Windows.Media.Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty AxisBrushProperty = DependencyProperty.Register(
            nameof(AxisBrush), typeof(System.Windows.Media.Brush), typeof(SparklineControl),
            new FrameworkPropertyMetadata(System.Windows.Media.Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public System.Windows.Media.Brush AxisBrush
        {
            get => (System.Windows.Media.Brush)GetValue(AxisBrushProperty);
            set => SetValue(AxisBrushProperty, value);
        }

        public static readonly DependencyProperty ShowGridlinesProperty = DependencyProperty.Register(
            nameof(ShowGridlines), typeof(bool), typeof(SparklineControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowGridlines
        {
            get => (bool)GetValue(ShowGridlinesProperty);
            set => SetValue(ShowGridlinesProperty, value);
        }

        public static readonly DependencyProperty GridlineBrushProperty = DependencyProperty.Register(
            nameof(GridlineBrush), typeof(System.Windows.Media.Brush), typeof(SparklineControl),
            new FrameworkPropertyMetadata(System.Windows.Media.Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

        public System.Windows.Media.Brush GridlineBrush
        {
            get => (System.Windows.Media.Brush)GetValue(GridlineBrushProperty);
            set => SetValue(GridlineBrushProperty, value);
        }

        public static readonly DependencyProperty YAxisLabelFormatProperty = DependencyProperty.Register(
            nameof(YAxisLabelFormat), typeof(string), typeof(SparklineControl),
            new FrameworkPropertyMetadata("{0}", FrameworkPropertyMetadataOptions.AffectsRender));

        // Use something like "{0}%" for percent charts
        public string YAxisLabelFormat
        {
            get => (string)GetValue(YAxisLabelFormatProperty);
            set => SetValue(YAxisLabelFormatProperty, value);
        }

        // Optional unit suffix for y-axis labels (e.g., "ms", "%"). If set, this overrides YAxisLabelFormat.
        public static readonly DependencyProperty YUnitProperty = DependencyProperty.Register(
            nameof(YUnit), typeof(string), typeof(SparklineControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public string YUnit
        {
            get => (string)GetValue(YUnitProperty);
            set => SetValue(YUnitProperty, value);
        }

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public static readonly DependencyProperty SplineEnabledProperty = DependencyProperty.Register(
            nameof(SplineEnabled), typeof(bool), typeof(SparklineControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool SplineEnabled
        {
            get => (bool)GetValue(SplineEnabledProperty);
            set => SetValue(SplineEnabledProperty, value);
        }

        // Back-compat alias for XAML using 'Thickness' instead of 'StrokeThickness'
        public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
            nameof(Thickness), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(1.5,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, e) => ((SparklineControl)d).StrokeThickness = (double)e.NewValue));

        public double Thickness
        {
            get => (double)GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        // Fade effect: older samples fade out towards the left of the window; newest at full opacity
        public static readonly DependencyProperty FadeEnabledProperty = DependencyProperty.Register(
            nameof(FadeEnabled), typeof(bool), typeof(SparklineControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool FadeEnabled
        {
            get => (bool)GetValue(FadeEnabledProperty);
            set => SetValue(FadeEnabledProperty, value);
        }

        #endregion

        private void OnCompositionTargetRendering(object? sender, EventArgs e)
        {
            // Frequent invalidation helps smooth the perceived scrolling when timestamps advance.
            // This is lightweight since OnRender is efficient and only uses current data snapshot.
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(new System.Windows.Point(0, 0), RenderSize);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var values = Values;
            if (values == null || values.Count == 0)
                return;

            var timestamps = Timestamps;
            bool hasTime = timestamps != null && timestamps.Count == values.Count && timestamps.Count > 0;

            double yAxisWidth = ShowYAxis ? Math.Max(0, YAxisWidth) : 0.0;
            double left = yAxisWidth;
            double top = Math.Max(0, PlotTopPadding);
            double bottom = Math.Max(0, PlotBottomPadding);

            var plotRect = new Rect(left, top, Math.Max(0, bounds.Width - left), Math.Max(0, bounds.Height - top - bottom));
            if (plotRect.Width <= 1 || plotRect.Height <= 1)
                return;

            // Determine X mapping
            double nowMs;
            double windowMs = Math.Max(1, WindowSeconds) * 1000.0;
            if (hasTime)
            {
                nowMs = timestamps![timestamps.Count - 1];
            }
            else
            {
                // Fabricate a time-base spacing if timestamps are missing
                nowMs = (values.Count - 1) * 1000.0; // 1s cadence assumption
            }

            // Build points within the window
            var pts = new List<System.Windows.Point>(values.Count);
            double minY = double.PositiveInfinity;
            double maxY = double.NegativeInfinity;

            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                double t = hasTime ? timestamps![i] : (i * 1000.0);

                double age = nowMs - t; // ms ago
                if (age < 0 || age > windowMs)
                    continue; // outside window (future or too old)

                double x = plotRect.Left + (1.0 - (age / windowMs)) * plotRect.Width;
                if (double.IsNaN(v) || double.IsInfinity(v))
                    continue;

                pts.Add(new System.Windows.Point(x, v));
                if (v < minY) minY = v;
                if (v > maxY) maxY = v;
            }

            if (pts.Count == 0)
                return;

            // Axis range
            if (ForceZeroBaseline && minY > 0)
                minY = 0;

            if (Math.Abs(maxY - minY) < 1e-9)
            {
                // Expand a degenerate range slightly to avoid division by zero
                double mid = minY;
                minY = mid - 0.5;
                maxY = mid + 0.5;
            }

            // Map Y from value space into plotRect (invert Y axis)
            double YToScreen(double y)
            {
                double tNorm = (y - minY) / (maxY - minY);
                return plotRect.Bottom - tNorm * plotRect.Height;
            }

            // Draw y-axis labels if enabled
            if (ShowYAxis)
            {
                DrawYAxis(dc, plotRect, minY, maxY);
            }

            // Gridlines
            if (ShowGridlines)
            {
                DrawGridlines(dc, plotRect);
            }

            // Map points to screen Y once
            var mappedPts = new List<System.Windows.Point>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                mappedPts.Add(new System.Windows.Point(pts[i].X, YToScreen(pts[i].Y)));
            }

            // Determine baseline (0 axis) in screen coords. If 0 not within range, use min edge.
            double zeroClamped = Math.Max(minY, Math.Min(maxY, 0));
            double baselineY = YToScreen(zeroClamped);

            // Draw area fade under the line (50% at line -> 0% at baseline)
            if (FadeEnabled && mappedPts.Count >= 2)
            {
                DrawAreaFade(dc, mappedPts, baselineY, plotRect);
            }

            // Build line pen (solid)
            var linePen = CreateLinePen();

            // Build geometry for the line on top
            if (mappedPts.Count == 1)
            {
                var p = mappedPts[0];
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(p, false, false);
                    gc.PolyLineTo(new[] { p }, true, true);
                }
                geo.Freeze();
                dc.DrawGeometry(null, linePen, geo);
                return;
            }

            if (SplineEnabled && mappedPts.Count >= 2)
            {
                var geo = BuildCatmullRomGeometryMapped(mappedPts);
                dc.DrawGeometry(null, linePen, geo);
            }
            else
            {
                var geo = new StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(mappedPts[0], false, false);
                    if (mappedPts.Count > 1)
                    {
                        gc.PolyLineTo(mappedPts.Skip(1).ToArray(), true, true);
                    }
                }
                geo.Freeze();
                dc.DrawGeometry(null, linePen, geo);
            }
        }

        private System.Windows.Media.Pen CreateLinePen()
        {
            var pen = new System.Windows.Media.Pen(LineBrush, StrokeThickness)
            {
                StartLineCap = System.Windows.Media.PenLineCap.Round,
                EndLineCap = System.Windows.Media.PenLineCap.Round
            };
            try { pen.Freeze(); } catch { }
            return pen;
        }

        private void DrawYAxis(DrawingContext dc, Rect plotRect, double minY, double maxY)
        {
            // Draw a thin vertical separator line between axis and plot (pixel snapped)
            var axisPen = new System.Windows.Media.Pen(AxisBrush, 1.0);
            double axisX = Math.Round(plotRect.Left) + 0.5; // crisp 1px line
            dc.DrawLine(axisPen, new System.Windows.Point(axisX, plotRect.Top), new System.Windows.Point(axisX, plotRect.Bottom));

            // Choose 3 ticks (min/mid/max) for readability
            int tickCount = 3;
            var typeface = new Typeface("Segoe UI");
            for (int i = 0; i < tickCount; i++)
            {
                double t = (double)i / (tickCount - 1);
                double val = minY + t * (maxY - minY);
                string text;
                if (!string.IsNullOrEmpty(YUnit))
                {
                    text = string.Concat(FormatNumber(val), YUnit);
                }
                else
                {
                    text = string.Format(CultureInfo.InvariantCulture, YAxisLabelFormat, FormatNumber(val));
                }
                var ft = new FormattedText(
                    text,
                    CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    typeface,
                    10.5,
                    AxisBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                // Right align within axis area; clamp vertically inside plot, snap to whole pixels for crisp text
                double x = Math.Max(0, plotRect.Left - 2) - ft.Width; // 2px padding from plot
                double y = plotRect.Bottom - t * plotRect.Height - ft.Height / 2.0;
                double yMin = plotRect.Top;
                double yMax = plotRect.Bottom - ft.Height;
                y = Math.Max(yMin, Math.Min(yMax, y));
                x = Math.Round(x);
                y = Math.Round(y);

                dc.DrawText(ft, new System.Windows.Point(x, y));
            }
        }

        private void DrawGridlines(DrawingContext dc, Rect plotRect)
        {
            int tickCount = 3;
            var pen = new System.Windows.Media.Pen(GridlineBrush, 0.5);
            for (int i = 0; i < tickCount; i++)
            {
                double t = (double)i / (tickCount - 1);
                double y = plotRect.Bottom - t * plotRect.Height;
                double ySnap = Math.Round(y) + 0.5; // crisp 1px
                double xL = Math.Round(plotRect.Left) + 0.5;
                double xR = Math.Round(plotRect.Right) + 0.5;
                dc.DrawLine(pen, new System.Windows.Point(xL, ySnap), new System.Windows.Point(xR, ySnap));
            }
        }

        private static string FormatNumber(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            // Prefer integer-like display when close to whole numbers
            if (Math.Abs(v - Math.Round(v)) < 1e-6) return ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
            // Otherwise short format
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private StreamGeometry BuildCatmullRomGeometry(IReadOnlyList<System.Windows.Point> source, Func<double, double> yMap)
        {
            // Map Y first while keeping X spacing
            var pts = source.Select(p => new System.Windows.Point(p.X, yMap(p.Y))).ToList();
            return BuildCatmullRomGeometryMapped(pts);
        }

        private StreamGeometry BuildCatmullRomGeometryMapped(IReadOnlyList<System.Windows.Point> pts)
        {
            var geo = new StreamGeometry();
            using (var gc = geo.Open())
            {
                if (pts.Count == 0)
                    return geo;

                gc.BeginFigure(pts[0], false, false);

                if (pts.Count == 2)
                {
                    gc.PolyLineTo(new[] { pts[1] }, true, true);
                }
                else
                {
                    // Catmull–Rom: for segment Pi to Pi+1 use P(i-1), Pi, Pi+1, P(i+2)
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        System.Windows.Point p0 = i == 0 ? pts[i] : pts[i - 1];
                        System.Windows.Point p1 = pts[i];
                        System.Windows.Point p2 = pts[i + 1];
                        System.Windows.Point p3 = (i + 2 < pts.Count) ? pts[i + 2] : pts[i + 1];

                        // Convert CR to Bezier for WPF PathSegment using standard matrix
                        var cp1 = new System.Windows.Point(
                            p1.X + (p2.X - p0.X) / 6.0,
                            p1.Y + (p2.Y - p0.Y) / 6.0);
                        var cp2 = new System.Windows.Point(
                            p2.X - (p3.X - p1.X) / 6.0,
                            p2.Y - (p3.Y - p1.Y) / 6.0);

                        gc.BezierTo(cp1, cp2, p2, true, true);
                    }
                }
            }
            geo.Freeze();
            return geo;
        }

        private void DrawAreaFade(DrawingContext dc, IReadOnlyList<System.Windows.Point> mappedPts, double baselineY, Rect plotRect)
        {
            // Build a filled geometry following the line, then down to baseline and back to start
            var area = new StreamGeometry();
            using (var gc = area.Open())
            {
                if (mappedPts.Count == 0) return;
                gc.BeginFigure(mappedPts[0], true, true);
                if (SplineEnabled && mappedPts.Count >= 2)
                {
                    for (int i = 0; i < mappedPts.Count - 1; i++)
                    {
                        System.Windows.Point p0 = i == 0 ? mappedPts[i] : mappedPts[i - 1];
                        System.Windows.Point p1 = mappedPts[i];
                        System.Windows.Point p2 = mappedPts[i + 1];
                        System.Windows.Point p3 = (i + 2 < mappedPts.Count) ? mappedPts[i + 2] : mappedPts[i + 1];

                        var cp1 = new System.Windows.Point(
                            p1.X + (p2.X - p0.X) / 6.0,
                            p1.Y + (p2.Y - p0.Y) / 6.0);
                        var cp2 = new System.Windows.Point(
                            p2.X - (p3.X - p1.X) / 6.0,
                            p2.Y - (p3.Y - p1.Y) / 6.0);

                        gc.BezierTo(cp1, cp2, p2, true, true);
                    }
                }
                else
                {
                    if (mappedPts.Count > 1)
                    {
                        gc.PolyLineTo(mappedPts.Skip(1).ToArray(), true, true);
                    }
                }

                // Down to baseline at end, then back to start baseline to close
                var last = mappedPts[mappedPts.Count - 1];
                var first = mappedPts[0];
                gc.LineTo(new System.Windows.Point(last.X, baselineY), true, true);
                gc.LineTo(new System.Windows.Point(first.X, baselineY), true, true);
            }
            area.Freeze();

            // Create a vertical gradient that is 50% at line (top of area) and 0% at baseline
            System.Windows.Media.Color baseColor;
            if (LineBrush is System.Windows.Media.SolidColorBrush scb)
            {
                baseColor = scb.Color;
            }
            else
            {
                baseColor = System.Windows.Media.Colors.DeepSkyBlue;
            }
            var fill = new System.Windows.Media.LinearGradientBrush(
                new System.Windows.Media.GradientStopCollection
                {
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb((byte)(baseColor.A * 0.5), baseColor.R, baseColor.G, baseColor.B), 0.0),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1.0)
                },
                new System.Windows.Point(0, 0),
                new System.Windows.Point(0, 1))
            {
                MappingMode = System.Windows.Media.BrushMappingMode.RelativeToBoundingBox
            };
            try { fill.Freeze(); } catch { }

            dc.DrawGeometry(fill, null, area);
        }
    }
}

