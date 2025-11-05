
namespace Wired.Nodes
{
    /// <summary>
    /// A remote receiver acts as a switch
    /// </summary>
    public class RemoteReceiver : Node
    {
        public bool IsOn { get; private set; } = true;
        public void Toggle(bool state)
        {
            IsOn = state;
            Plugin.Instance.UpdateAllNetworks();
        }

        public override void IncreaseVoltage(uint amount)
        {
            if (!IsOn) return;
        }

        public override void DecreaseVoltage(uint amount)
        {
            if (!IsOn) return;
        }
    }
}