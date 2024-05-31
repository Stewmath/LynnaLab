namespace LynnaLib
{
    public class MyColor
    {
        int r, g, b, a;

        public int R
        {
            get { return r; }
        }
        public int G
        {
            get { return g; }
        }
        public int B
        {
            get { return b; }
        }
        public int A
        {
            get { return a; }
        }

        private MyColor() { }

        public static MyColor FromRgb(int r, int g, int b)
        {
            return FromRgba(r, g, b, 255);
        }

        public static MyColor FromRgba(int r, int g, int b, int a)
        {
            MyColor c = new MyColor();
            c.r = r;
            c.g = g;
            c.b = b;
            c.a = a;
            return c;
        }

        public static implicit operator Cairo.Color(MyColor c)
        {
            return new Cairo.Color(c.r / 255.0, c.g / 255.0, c.b / 255.0, c.a / 255.0);
        }


        // Some colors copied over from System.Drawing to keep things consistent
        public static readonly MyColor Black = FromRgb(0x00, 0x00, 0x00);
        public static readonly MyColor Blue = FromRgb(0x00, 0x00, 0xff);
        public static readonly MyColor Cyan = FromRgb(0x00, 0xff, 0xff);
        public static readonly MyColor DarkOrange = FromRgb(0xff, 0x8c, 0x00);
        public static readonly MyColor Gray = FromRgb(0x80, 0x80, 0x80);
        public static readonly MyColor Green = FromRgb(0x00, 0x80, 0x00);
        public static readonly MyColor Lime = FromRgb(0x00, 0xff, 0x00);
        public static readonly MyColor Purple = FromRgb(0x80, 0x00, 0x80);
        public static readonly MyColor Yellow = FromRgb(0xff, 0xff, 0x00);
    }
}
