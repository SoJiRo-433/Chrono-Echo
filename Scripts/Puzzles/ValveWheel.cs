/// <summary>A six-second held valve that suppresses its conduit section for fifteen seconds.</summary>
public partial class ValveWheel : Switch
{
    public override void _Ready()
    {
        RequiredHoldSeconds = 6f;
        HoldActivationSeconds = 15f;
        RequireHeldInteraction = true;
        base._Ready();
    }
}
