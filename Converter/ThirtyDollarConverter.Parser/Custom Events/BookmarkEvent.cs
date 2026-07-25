namespace ThirtyDollarParser.Custom_Events;

public class BookmarkEvent : BaseEvent, IHiddenEvent, ICustomActionEvent
{
    public BookmarkEvent()
    {
        SoundEvent = "#bookmark";
    }

    public override BookmarkEvent Copy()
    {
        return new BookmarkEvent
        {
            Value = Value
        };
    }
}