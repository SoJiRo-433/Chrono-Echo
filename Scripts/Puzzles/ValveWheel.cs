/// <summary>A tap-to-close valve that permanently suppresses its conduit section.</summary>
public partial class ValveWheel : Switch
{
    public override void _Ready()
    {
        StaysOn = true;
        RequiredHoldSeconds = 0f;
        HoldActivationSeconds = 0f;
        RequireHeldInteraction = false;
        PersistActivation = true;
        base._Ready();
    }
}
