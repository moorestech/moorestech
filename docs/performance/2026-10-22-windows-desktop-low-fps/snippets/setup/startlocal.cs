using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
var s = Object.FindFirstObjectByType<Client.MainMenu.StartLocal>(FindObjectsInactive.Include);
var b = (Button)typeof(Client.MainMenu.StartLocal).GetField("startLocalButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s);
b.onClick.Invoke();
return "start local interactable=" + b.interactable;
