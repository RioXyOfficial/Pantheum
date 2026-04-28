using Mirror;
using UnityEngine;

namespace Pantheum.Network
{
    public class NetworkLobbyUI : MonoBehaviour
    {
        private string _ip = "127.0.0.1";

        private GUIStyle _boxStyle;
        private GUIStyle _btnStyle;

        private void OnGUI()
        {
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box)    { fontSize = 14 };
                _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            }

            bool isActive = NetworkServer.active || NetworkClient.active;

            if (isActive)
            {
                GUILayout.BeginArea(new Rect(10, 10, 260, 60));
                string state = NetworkServer.active
                    ? (NetworkServer.connections.Count >= 2 ? "En partie (2 joueurs)" : "Hébergement - en attente d'un joueur…")
                    : "Connecté";
                GUILayout.Label(state, _boxStyle, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Déconnecter", _btnStyle))
                {
                    if (NetworkServer.active) NetworkManager.singleton.StopHost();
                    else                      NetworkManager.singleton.StopClient();
                }
                GUILayout.EndArea();
                return;
            }

            float w = 240f, h = 150f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h));

            GUILayout.Label("Pantheum", _boxStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("Multijoueur LAN", _boxStyle, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Héberger la partie", _btnStyle))
                NetworkManager.singleton.StartHost();

            GUILayout.Label("IP du serveur:", _boxStyle, GUILayout.ExpandWidth(true));
            _ip = GUILayout.TextField(_ip, _btnStyle);

            if (GUILayout.Button("Rejoindre", _btnStyle))
            {
                NetworkManager.singleton.networkAddress = _ip;
                NetworkManager.singleton.StartClient();
            }

            GUILayout.EndArea();
        }
    }
}
