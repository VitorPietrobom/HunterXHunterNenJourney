namespace HunterAwakening
{
    /// <summary>The notice board at the training grounds: opens the Hunter's Guide (also a page of the dashboard, Tab).</summary>
    public class GuideBoard : Interactable
    {
        public override string GetPrompt() => "[E] Read the Hunter's Guide — Nen, training, combat and the hunt (also in the dashboard, Tab)";

        public override void Interact(PlayerContext player) => Session.OpenGuide();
    }
}
