﻿using System;
using COG.Utils;
using NLua;

namespace COG.Plugin.Loader.Controller.Classes.Player;

public class PlayerController
{
    public Lua Lua { get; }
    public IPlugin Plugin { get; }
    
    public PlayerController(Lua lua, IPlugin plugin)
    {
        Lua = lua;
        Plugin = plugin;
    }

    public COG.Role.Role GetRoleByPlayer(PlayerControl playerControl) => playerControl.GetRoleInstance()!;

    public void KillPlayer(PlayerControl playerControl) =>
        playerControl.MurderPlayer(playerControl, GameUtils.DefaultFlag);

    public PlayerControl GetRandomPlayer()
    {
        var random = new Random();
        var players = PlayerUtils.GetAllPlayers();
        return players[random.Next(0, players.Count - 1)];
    }
}