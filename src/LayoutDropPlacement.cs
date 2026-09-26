namespace Ziopuzzle.CustomButton;

public static class LayoutDropPlacement
{
    public static string Header(bool container, bool root, double relativeY)
    {
        if (root && container) return "inside";
        if (!container) return relativeY < .5 ? "before" : "after";
        return relativeY < .25 ? "before" : relativeY > .75 ? "after" : "inside";
    }
}
