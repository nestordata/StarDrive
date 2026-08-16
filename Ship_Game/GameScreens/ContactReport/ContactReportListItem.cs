using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Empires.Components;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game.GameScreens.ContactReport;

public sealed class ContactReportListItem : ScrollListItem<ContactReportListItem>
{
    public readonly ContactReportDesign Entry;
    readonly Graphics.Font Font = Fonts.Arial12Bold;
    readonly Color RowColor;
    readonly UIPanel FlagIcon;
    readonly UIPanel DesignIcon;

    public ContactReportListItem(ContactReportDesign entry)
    {
        Entry    = entry;
        RowColor = entry.Loyalty?.EmpireColor ?? Color.LightGray;

        if (entry.Loyalty != null)
        {
            FlagIcon = Add(new UIPanel(Pos, ResourceManager.Flag(entry.Loyalty.data.Traits.FlagIndex),
                                       entry.Loyalty.EmpireColor));
            FlagIcon.Size = new Vector2(32, 32);
        }

        IShipDesign design = entry.ResolveDesign();
        SubTexture iconTex = design?.Icon;
        if (iconTex == null && entry.IconPath.NotEmpty() && ResourceManager.TextureLoaded(entry.IconPath))
            iconTex = ResourceManager.Texture(entry.IconPath);
        if (iconTex != null)
        {
            DesignIcon = Add(new UIPanel(Pos, iconTex));
            DesignIcon.Size = new Vector2(48, 48);
        }

        string role = entry.Loyalty != null
            ? Localizer.GetRole(entry.Role, entry.Loyalty)
            : entry.Role.ToString();
        string scan = entry.InternalsKnown
            ? Localizer.Token(GameText.ContactReportInternalsKnown)
            : Localizer.Token(GameText.ContactReportHullOnly);

        AddColumn(entry.DesignName, 240, 140, Colors.Cream);
        AddColumn(role, 130, 390, Color.Orange);
        AddColumn($"x{entry.Count}", 60, 530, Color.LightBlue);
        UILabel scanLabel = AddColumn(scan, 130, 600, entry.InternalsKnown ? Color.LightGreen : Color.Gray);
        if (!entry.InternalsKnown)
            scanLabel.Tooltip = GameText.ContactReportNoInternals;
    }

    UILabel AddColumn(string text, float sizeX, float relativeX, Color color)
    {
        string parsed = Font.ParseText(text, sizeX - 16);
        UILabel label = Add(new UILabel(parsed, Font, color));
        label.Size = new Vector2(sizeX, 72);
        label.TextAlign = TextAlign.VerticalCenter;
        label.SetLocalPos(relativeX, 0);
        return label;
    }

    public override void Draw(SpriteBatch batch, DrawTimes elapsed)
    {
        Color borderColor = Dim(RowColor, 3);
        batch.FillRectangle(Rect, Dim(RowColor, 10));
        batch.DrawRectangle(Rect, borderColor);

        if (FlagIcon != null)
            FlagIcon.Pos = new Vector2(Pos.X + 8, Pos.Y + 20);
        if (DesignIcon != null)
            DesignIcon.Pos = new Vector2(Pos.X + 48, Pos.Y + 12);

        base.Draw(batch, elapsed);
    }

    static Color Dim(Color color, int divider)
    {
        return new Color((byte)(color.R / divider),
                         (byte)(color.G / divider),
                         (byte)(color.B / divider));
    }
}
