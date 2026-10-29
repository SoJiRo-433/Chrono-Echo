using Godot;

public partial class MainMenu : Control
{
    private VideoStreamPlayer _video;
    private Button _btnStart;
    private Button _btnLoadGame;
    private Button _btnNewGame;
    private Button _btnCGs;
    private Button _btnQuit;

    public override void _Ready()
    {
        // Lets the Esc/pause menu know it should stay closed here
        AddToGroup("main_menu");
        GetTree().Paused = false;

        // Background video
        _video = GetNode<VideoStreamPlayer>("menuAnimation");
        _video.Loop = true;
        _video.Play();

        // Buttons (paths are relative to Main Menu)
        _btnStart    = GetNode<Button>("grpMenu/btnStart");
        _btnLoadGame = GetNode<Button>("grpMenu/btnLoadGame");
        _btnNewGame  = GetNode<Button>("grpMenu/btnNewGame");
        _btnCGs      = GetNode<Button>("grpMenu/CGs");
        _btnQuit     = GetNode<Button>("grpMenu/Quit");

        _btnStart.Pressed    += OnStartPressed;
        _btnLoadGame.Pressed += OnLoadGamePressed;
        _btnNewGame.Pressed  += OnNewGamePressed;
        _btnCGs.Pressed      += OnCGsPressed;
        _btnQuit.Pressed     += OnQuitPressed;

        // "Start" becomes "Continue" once a game exists
        _btnStart.Text = SaveManager.HasGame ? "Continue" : "Start";

        _btnStart.GrabFocus();
    }

    private void OnStartPressed()
    {
        if (SaveManager.HasGame)
            SaveManager.LoadSlot(GetTree(), SaveManager.LastSlot); // Continue
        else
            SaveManager.NewGame(GetTree());                        // Start = new game
    }

    private void OnNewGamePressed()
    {
        if (!SaveManager.HasGame)
        {
            SaveManager.NewGame(GetTree());
            return;
        }

        UiHelper.Confirm(
            this,
            "New Game",
            "You already have a game in progress.\nStarting a new game will overwrite it. Continue?",
            () => SaveManager.NewGame(GetTree()),
            "Overwrite");
    }

    private void OnLoadGamePressed()
    {
        SlotPicker.Open(this, SlotPickerMode.Load);
    }

    private void OnCGsPressed()
    {
        UiHelper.Notify(this, "CGs", "Coming soon");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
