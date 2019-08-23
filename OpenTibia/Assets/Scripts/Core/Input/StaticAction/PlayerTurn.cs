﻿namespace OpenTibiaUnity.Core.Input.StaticAction
{
    public class PlayerTurn : StaticAction
    {
        private Direction _direction;
        public PlayerTurn(int id, string label, uint eventMask, Direction direction) : base(id, label, eventMask, false) {
            if (direction < Direction.North || direction > Direction.West)
                throw new System.ArgumentException("PlayerTurn.PlayerTurn: Invalid direction.");

            _direction = direction;
        }

        public override bool Perform(bool repeat = false) {
            var protocolGame = OpenTibiaUnity.ProtocolGame;
            if (!!protocolGame && protocolGame.IsGameRunning) {
                switch (_direction) {
                    case Direction.North:
                        protocolGame.SendTurnNorth();
                        break;
                    case Direction.East:
                        protocolGame.SendTurnEast();
                        break;
                    case Direction.South:
                        protocolGame.SendTurnSouth();
                        break;
                    case Direction.West:
                        protocolGame.SendTurnWest();
                        break;
                }

                return true;
            }

            return false;
        }

        public override IAction Clone() {
            return new PlayerTurn(_id, _label, _eventMask, _direction);
        }
    }
}
