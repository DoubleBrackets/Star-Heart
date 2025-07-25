using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;

namespace Protag
{
    public class NetworkProtag : NetworkBehaviour
    {
        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        private List<Color> _playerColors;

        [SerializeField]
        private TrailRenderer _trailRenderer;

        public int PlayerNumber => _playerNumber;

        public Color PlayerColor => _playerColors[_playerNumber];

        /// <summary>
        ///     The player number assigned to this protag, either 0 or 1
        /// </summary>
        private int _playerNumber;

        public override void WritePayload(NetworkConnection connection, Writer writer)
        {
            // Not predicted, we know this is from server
            if (ProtagManager.Instance.GetProtagData(Owner, out ProtagManager.ProtagData data))
            {
                writer.WriteInt32(data.PlayerNumber);
            }
            else
            {
                writer.WriteInt32(0);
            }
        }

        public override void ReadPayload(NetworkConnection connection, Reader reader)
        {
            _playerNumber = reader.ReadInt32();

            _spriteRenderer.color = _playerColors[_playerNumber];
            if (_trailRenderer != null)
            {
                _trailRenderer.startColor = _playerColors[_playerNumber];
                _trailRenderer.endColor = _playerColors[_playerNumber];
            }
        }

        public override void OnStartNetwork()
        {
            RenameGameObject(NetworkObject.gameObject);
        }

        private void RenameGameObject(GameObject target)
        {
            target.name = "Protag";
            target.name += $"[Player={PlayerNumber}]";
            if (Owner.IsHost)
            {
                target.name += "[Host]";
            }
            else
            {
                target.name += "[Client]";
            }

            target.name += $"[Owner={OwnerId}]";
        }
    }
}