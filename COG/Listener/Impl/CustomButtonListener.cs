﻿using COG.UI.CustomButton;

namespace COG.Listener.Impl;

internal class CustomButtonListener : IListener
{
    public void OnHudStart(HudManager? hud)
    {
        CustomButton.Inited = false;
        CustomButton.Init(hud);
    }

    public void OnHudUpdate(HudManager manager)
    {
        if (!CustomButton.Inited) return;
        CustomButton.GetAllButtons();
        CustomButton.ArrangePosition();
        foreach (var button in CustomButtonManager.GetManager().GetButtons()) button.Update();
    }

    public void OnHudDestroy(HudManager hud)
    {
        CustomButton.Inited = false;
    }
}