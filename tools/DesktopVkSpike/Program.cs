using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DesktopVkSpike;

class SpikeGame : Game
{
    readonly GraphicsDeviceManager _gdm;

    public SpikeGame()
    {
        _gdm = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        Window.Title = "StarDrive DesktopVK Spike";
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        base.Draw(gameTime);
    }
}

static class Program
{
    static void Main()
    {
        Console.WriteLine("Starting DesktopVK spike...");
        using var game = new SpikeGame();
        game.Run();
        Console.WriteLine("Spike exited cleanly.");
    }
}
