using System;
using System.Collections.Generic;

using LynnaLib;

namespace LynnaLab
{
    public static class CairoHelper
    {
        public static void DrawText(Cairo.Context cr, string text, int size, Cairo.Rectangle rect)
        {
            cr.Save();
            cr.Translate(rect.X, rect.Y);
            using (var context = Pango.CairoHelper.CreateContext(cr))
            using (var layout = new Pango.Layout(context))
            {
                layout.Width = Pango.Units.FromPixels((int)rect.Width);
                layout.Height = Pango.Units.FromPixels((int)rect.Height);
                layout.Alignment = Pango.Alignment.Center;

                // TODO: install the font on the system
                layout.FontDescription = Pango.FontDescription.FromString("ZeldaOracles " + size);
                //layout.FontDescription.Weight = (Pango.Weight)10000;

                layout.SetText(text);

                // Center vertically
                int pixelWidth, pixelHeight;
                layout.GetPixelSize(out pixelWidth, out pixelHeight);
                cr.Translate(0, ((int)rect.Height - pixelHeight) / 2.0);

                Pango.CairoHelper.ShowLayout(cr, layout);
            }
            cr.Restore();
        }

        public static void DrawText(Cairo.Context cr, string text, int size, int x, int y, int width, int height)
        {
            DrawText(cr, text, size, new Cairo.Rectangle(x, y, width, height));
        }

        // Draws the outline of a rectangle without drawing anything outside of it. The "lineWidth"
        // is all drawn inside the rectangle instead of being equally outside and inside.
        public static void DrawRectOutline(Cairo.Context cr, double lineWidth, Cairo.Rectangle rect)
        {
            cr.LineWidth = lineWidth;
            cr.Rectangle(rect.X + lineWidth / 2, rect.Y + lineWidth / 2,
                    rect.Width - lineWidth, rect.Height - lineWidth);
            cr.Stroke();
        }

        public static void DrawRectOutline(Cairo.Context cr, double lineWidth, double x, double y, double width, double height)
        {
            DrawRectOutline(cr, lineWidth, new Cairo.Rectangle(x, y, width, height));
        }

        public static bool PointInRect(Cairo.Point p, Cairo.Rectangle rect)
        {
            return p.X >= rect.X && p.Y >= rect.Y
                && p.X < rect.X + rect.Width && p.Y < rect.Y + rect.Height;
        }

        public static bool PointInRect(int x, int y, Cairo.Rectangle rect)
        {
            return PointInRect(new Cairo.Point(x, y), rect);
        }

        public static bool PointInRect(int x, int y, int left, int top, int width, int height)
        {
            return PointInRect(new Cairo.Point(x, y), new Cairo.Rectangle(left, top, width, height));
        }

        public static bool RectsOverlap(Cairo.Rectangle rect1, Cairo.Rectangle rect2)
        {
            double r1 = rect1.X + rect1.Width;
            double r2 = rect2.X + rect2.Width;
            double b1 = rect1.Y + rect1.Height;
            double b2 = rect2.Y + rect2.Height;

            return r1 > rect2.X && r2 > rect1.X
                && b1 > rect2.Y && b2 > rect1.Y;
        }


        // Extension methods

        /// Convert MyColor to Gdk.Color
        public static Gdk.RGBA ToGdk(this Color self)
        {
            return new Gdk.RGBA {
                    Red = self.R / 255.0,
                    Green = self.G / 255.0,
                    Blue = self.B / 255.0,
                    Alpha = self.A / 255.0
                };
        }
        /// Convert Gdk.Color to MyColor
        public static Color FromGdk(this Gdk.RGBA self)
        {
            return Color.FromRgba(
                (int)(self.Red * 255),
                (int)(self.Green * 255),
                (int)(self.Blue * 255),
                (int)(self.Alpha * 255));
        }
    }
}
