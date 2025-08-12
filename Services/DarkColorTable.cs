using System.Drawing;
using System.Windows.Forms;

namespace Netwatch.Services
{
    // Custom color table to style WinForms context menu to match WPF dark theme
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color Card = ColorTranslator.FromHtml("#1E1E1F");
        private static readonly Color Card2 = ColorTranslator.FromHtml("#222224");
        private static readonly Color Divider = ColorTranslator.FromHtml("#22000000");
        private static readonly Color Highlight = ColorTranslator.FromHtml("#2A2A2A");
        private static readonly Color Text = ColorTranslator.FromHtml("#F7F7F8");
        private static readonly Color Muted = ColorTranslator.FromHtml("#AAAAAA");

        public override Color MenuBorder => Divider;
        public override Color MenuItemBorder => Divider;
        public override Color ToolStripDropDownBackground => Card;
        public override Color ImageMarginGradientBegin => Card;
        public override Color ImageMarginGradientMiddle => Card;
        public override Color ImageMarginGradientEnd => Card;
        public override Color MenuItemSelected => Highlight;
        public override Color MenuItemSelectedGradientBegin => Highlight;
        public override Color MenuItemSelectedGradientEnd => Highlight;
        public override Color MenuItemPressedGradientBegin => Card2;
        public override Color MenuItemPressedGradientEnd => Card2;
        public override Color SeparatorDark => Divider;
        public override Color SeparatorLight => Divider;
    }
}

