#if UNITY_EDITOR
public static class AnimatorStateEventTimelineLauncher
{
    public static void Open(AnimatorStateEventManager manager)
    {
        AnimatorStateEventTimelineWindow.Open(manager);
    }
}
#endif