namespace OpenTibiaUnity.Core.Input.StaticAction
{
    public class ChatNextChannel : StaticAction
    {
        public ChatNextChannel(int id, string label, InputEvent eventMask) : base(id, label, eventMask, false) { }

        public override bool Perform(bool repeat = false) {
            OpenTibiaUnity.GameManager.onRequestChatNextChannel.Invoke();
            return true;
        }

        public override IAction Clone() {
            return new ChatNextChannel(_id, _label, _eventMask);
        }
    }
}
