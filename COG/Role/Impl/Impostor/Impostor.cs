﻿using AmongUs.GameOptions;
using COG.Config.Impl;
using COG.Listener;
using UnityEngine;

namespace COG.Role.Impl.Impostor;

public class Impostor : Role
{
    public Impostor() : base(LanguageConfig.Instance.ImpostorName, Palette.ImpostorRed, CampType.Impostor, true)
    {
        CanKill = true;
        CanVent = true;
        BaseRole = true;
        CanSabotage = true;
        BaseRoleType = RoleTypes.Impostor;
        Description = LanguageConfig.Instance.ImpostorDescription;
    }

    public override IListener GetListener(PlayerControl player)
    {
        return IListener.Empty;
    }
}