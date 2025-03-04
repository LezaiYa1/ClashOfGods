using System.Linq;
using COG.Role;
using COG.Rpc;
using COG.UI.CustomOption;
using COG.Utils;

namespace COG.Listener.Impl;

public class RpcListener : IListener
{
    public void OnRPCReceived(byte callId, MessageReader reader)
    {
    }

    public void OnLobbyRPCReceived(byte callId, MessageReader reader)
    {
    }

    public void AfterRPCReceived(byte callId, MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return; // 是房主就返回
        var knownRpc = (KnownRpc)callId;

        switch (knownRpc)
        {
            case KnownRpc.UpdateOption:
                var originalUpdateOptionString = reader.ReadString()!;
                var contextsList = originalUpdateOptionString.Split("|");
                for (var i = 0; i < CustomOption.Options.Count; i++)
                {
                    var option = CustomOption.Options[i];
                    if (option == null) continue;
                    if (option.ID != int.Parse(contextsList[0])) continue;
                    option.Selection = int.Parse(contextsList[1]);
                    CustomOption.Options[i] = option;
                }
                break;
            case KnownRpc.ShareOptions:
                var originalString = reader.ReadString();
                Main.Logger.LogInfo("Received options string => " + originalString);
                foreach (var s in originalString.Split(","))
                {
                    var contexts = s.Split("|");
                    var id = int.Parse(contexts[0]);
                    var selection = int.Parse(contexts[1]);
                    
                    for (var i = 0; i < CustomOption.Options.Count; i++)
                    {
                        var option = CustomOption.Options[i];
                        if (option == null) continue;
                        if (option.ID != id) continue;
                        Main.Logger.LogInfo($"Changed {option.Name}({option.ID})'s selection to {selection}(before: {option.Selection})");
                        option.Selection = selection;
                        CustomOption.Options[i] = option;
                    }
                }
                break;
            case KnownRpc.SetRole:
                {
                    var playerId = reader.ReadByte();
                    var roleId = reader.ReadPackedUInt32();
                    GameUtils.SetCustomRole(PlayerUtils.GetPlayerById(playerId)!, Role.RoleManager.GetManager().GetRoleById(roleId)!);
                }
                break;
        }
    }
}