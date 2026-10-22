using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
var popup = Object.FindFirstObjectByType<Client.MainMenu.Playtest.CrashReportPopup>(FindObjectsInactive.Include);
string r = "popup=" + (popup ? popup.gameObject.activeInHierarchy.ToString() : "none");
if (popup && popup.gameObject.activeInHierarchy) {
  var skip = (Button)typeof(Client.MainMenu.Playtest.CrashReportPopup).GetField("skipButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(popup);
  skip.onClick.Invoke(); r += " skipped";
}
return r;
