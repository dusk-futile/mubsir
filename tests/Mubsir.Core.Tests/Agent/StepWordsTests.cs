using Mubsir.Core.Agent;
using Mubsir.Core.Common;

namespace Mubsir.Core.Tests.Agent;

/// <summary>After a step, Mubsir says what it finished, so the person hears the result, not just "done".</summary>
public sealed class StepWordsTests
{
    [Theory]
    [InlineData("Opening Settings", "Opened Settings.")]
    [InlineData("Opening Google, searching for \"prayer times\"", "Opened Google, searched for \"prayer times\".")]
    [InlineData("Checking Windows Update; this can take a minute", "Checked Windows Update.")]
    [InlineData("Pressing the button \"Install all\"", "Pressed the button \"Install all\".")]
    [InlineData("Clicking \"Reading list\"", "Clicked \"Reading list\".")]
    [InlineData("Typing in \"Search\"", "Typed in \"Search\".")]
    [InlineData("Going to the top", "Went to the top.")]
    [InlineData("Moving the file", "Moved the file.")]
    [InlineData("Setting the volume", "Set the volume.")]
    public void English_steps_are_said_in_the_past(string started, string finished)
    {
        Assert.Equal(finished, StepWords.Finished(started, Lang.English));
    }

    [Theory]
    [InlineData("أفتح الإعدادات", "فتحت الإعدادات.")]
    [InlineData("أفتح جوجل وأبحث عن «مواعيد الصلاة»", "فتحت جوجل وبحثت عن «مواعيد الصلاة».")]
    [InlineData("أفحص تحديثات ويندوز، قد يستغرق ذلك دقيقة", "فحصت تحديثات ويندوز.")]
    [InlineData("أضغط «أفتح الملف»", "ضغطت «أفتح الملف».")]
    [InlineData("ألتقط صورة للشاشة وأصفها", "التقطت صورة للشاشة ووصفتها.")]
    [InlineData("أنقل الملف", "نقلت الملف.")]
    public void Arabic_steps_are_said_in_the_past(string started, string finished)
    {
        Assert.Equal(finished, StepWords.Finished(started, Lang.Arabic));
    }

    [Theory]
    [InlineData("Next", Lang.English, "Done.")]
    [InlineData("Play or pause", Lang.English, "Done.")]
    [InlineData("التالي", Lang.Arabic, "تم.")]
    [InlineData("", Lang.English, "Done.")]
    [InlineData(null, Lang.Arabic, "تم.")]
    public void Anything_else_is_just_done(string? started, Lang lang, string finished)
    {
        Assert.Equal(finished, StepWords.Finished(started, lang));
    }
}
