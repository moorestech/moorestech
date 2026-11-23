// 計測の前にプレイヤーを決まった位置へ移し、カメラを決まった向き・距離に合わせて、それらとプレビュー半径内のブロック数を返す
// 位置と視点はベースラインワールドで設置モードが重い場所（2026-11-15 ユーザー指定。三人称・距離 3.5m・下へ 2.52°・方位 321.86°）
// Move the player to the fixed spot, set the camera to the fixed direction and distance, and return them with the count of blocks within the preview radius
// The spot and view are where build mode is heavy in the baseline world (chosen by the user on 2026-11-15: third person, 3.5 m, pitch 2.52, yaw 321.86)
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Control;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using UnityEngine;

var spot = new Vector3(622.21f, 82.99f, 420.90f);
PlayerSystemContainer.Instance.PlayerObjectController.SetPlayerPosition(spot);
ClientContext.VanillaApi.SendOnly.SendPlayerPosition(spot);

// カメラは三人称・距離 3.5m にし、向きは現在と目標の両方へ入れる（目標だけだと補間で数フレームずれる）
// Set the camera to third person at 3.5 m, and write the direction to both the current and target rotation (target alone lags for a few frames)
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
var camera = Object.FindFirstObjectByType<InGameCameraController>();
camera.SetFirstPersonMode(false);
var view = Quaternion.Euler(2.52f, 321.86f, 0f);
camera.transform.rotation = view;
typeof(InGameCameraController).GetField("_targetRotation", bf).SetValue(camera, view);
var framing = typeof(InGameCameraController).GetField("_cinemachineFraming", bf).GetValue(camera);
framing.GetType().GetField("m_CameraDistance").SetValue(framing, 3.5f);

// 半径の判定は次の Tick で動くので、ここでは中心からの距離で数える（判定と同じ 20m。チャンク 4m 単位の判定とは端で少し違う）
// The radius evaluation runs on the next Tick, so count by distance here (same 20 m; differs slightly at the edge from the 4 m chunk evaluation)
var blocks = ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values;
var within = blocks.Count(b => Vector3.Distance(b.transform.position, spot) <= 20f);
return "player=" + PlayerSystemContainer.Instance.PlayerObjectController.Position.ToString("F2") + " cameraEuler=" + camera.transform.eulerAngles.ToString("F2")
       + " cameraDistance=" + camera.CameraDistance.ToString("F2") + " fov=" + camera.Camera.fieldOfView.ToString("F1") + " blocksWithin20m=" + within;
