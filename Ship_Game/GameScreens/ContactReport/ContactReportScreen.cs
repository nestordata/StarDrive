using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Empires.Components;
using Ship_Game.ExtensionMethods;
using Ship_Game.GameScreens.ShipDesign;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens.ContactReport;

public sealed class ContactReportScreen : GameScreen
{
    readonly Menu2 Window;
    readonly Color Cream = Colors.Cream;
    readonly Empires.Components.ContactReport Report;
    readonly ScrollList<ContactReportListItem> DesignList;
    readonly ShipInfoOverlayComponent Overlay;
    readonly UILabel InternalsHint;
    readonly Graphics.Font LargeFont = Fonts.Arial20Bold;

    public ContactReportScreen(UniverseScreen screen, Empires.Components.ContactReport report)
        : base(screen, toPause: null)
    {
        Report            = report;
        IsPopup           = true;
        TransitionOnTime  = 0.25f;
        TransitionOffTime = 0.25f;

        Window = Add(new Menu2(new Rectangle(ScreenWidth / 2 - 600, ScreenHeight / 2 - 300, 1200, 540)));
        int x  = (int)Window.X + 20;
        int y  = (int)Window.Y + 70;
        int w  = 760;
        int h  = (int)Window.Height - 80;

        DesignList = Add(new ScrollList<ContactReportListItem>(new RectF(x, y, w, h), 72));
        DesignList.EnableItemHighlight = true;
        Overlay = Add(new ShipInfoOverlayComponent(this, screen.UState));
        InternalsHint = Add(new UILabel(GameText.ContactReportNoInternals, Fonts.Arial12Bold, Color.Gray));
        InternalsHint.Size = new Vector2(320, 80);
        InternalsHint.Visible = false;

        UILabel designLabel = Add(new UILabel(GameText.ContactReportReviewDesigns, LargeFont, Cream));
        UILabel roleLabel   = Add(new UILabel(GameText.Role, LargeFont, Cream));
        UILabel countLabel  = Add(new UILabel(GameText.ContactReportShips, LargeFont, Cream));
        designLabel.Size    = new Vector2(260, 20);
        roleLabel.Size      = new Vector2(140, 20);
        countLabel.Size     = new Vector2(70, 20);
        designLabel.Pos     = new Vector2(x + 140, y - 10);
        roleLabel.Pos       = new Vector2(x + 410, y - 10);
        countLabel.Pos      = new Vector2(x + 560, y - 10);
    }

    void PopulateDesigns()
    {
        foreach (ContactReportDesign entry in Report.Designs)
            DesignList.AddItem(new ContactReportListItem(entry));
    }

    void ShowDesign(ContactReportDesign entry)
    {
        if (entry == null)
        {
            Overlay.Hide();
            InternalsHint.Visible = false;
            return;
        }

        IShipDesign design = entry.ResolveDesign();
        if (design == null)
        {
            Overlay.Hide();
            InternalsHint.Visible = false;
            return;
        }

        float size = LowRes ? 272 : 340;
        var pos = new Vector2(Window.Right - size - 40, Window.Y + 90);
        Overlay.ShowAt(pos, design, showInternals: entry.InternalsKnown);
        InternalsHint.Visible = !entry.InternalsKnown;
        InternalsHint.Pos = new Vector2(pos.X, pos.Y + size + 8);
    }

    public override void LoadContent()
    {
        CloseButton(Window.Menu.Right - 40, Window.Menu.Y + 20);
        string title = $"{Localizer.Token(GameText.ContactReport)}  {Report.LocationName}  {Report.StarDate.StarDateString()}";
        Vector2 menuPos = new Vector2(Window.Menu.CenterTextX(title, Fonts.Laserian14), Window.Menu.Y + 30);
        Label(menuPos, title, Fonts.Laserian14, Cream);

        PopulateDesigns();
        DesignList.OnHovered = item => ShowDesign(item?.Entry);
        DesignList.OnClick   = item => ShowDesign(item?.Entry);

        if (Report.Designs.NotEmpty)
            ShowDesign(Report.Designs[0]);

        base.LoadContent();
    }

    public override void Draw(SpriteBatch batch, DrawTimes elapsed)
    {
        ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
        batch.SafeBegin();
        base.Draw(batch, elapsed);
        batch.SafeEnd();
    }

    public override bool HandleInput(InputState input)
    {
        if (input.Escaped || input.RightMouseClick)
        {
            ExitScreen();
            return true;
        }
        return base.HandleInput(input);
    }
}
