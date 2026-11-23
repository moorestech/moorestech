using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using UnityEngine;
var spot = new Vector3(622.21f, 82.99f, 420.90f);
PlayerSystemContainer.Instance.PlayerObjectController.SetPlayerPosition(spot);
ClientContext.VanillaApi.SendOnly.SendPlayerPosition(spot);
return "teleported " + PlayerSystemContainer.Instance.PlayerObjectController.Position.ToString("F2") + " camEuler=" + Camera.main.transform.eulerAngles.ToString("F1");
