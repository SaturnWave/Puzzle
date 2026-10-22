// The iPhone's Taptic Engine for Haptics.cs (Scripts/Sound): one tap of UIKit's impact feedback, in the style the
// game asks for (UIImpactFeedbackStyle: 0 light, 1 medium, 2 heavy). One generator per style lives as long as the
// game and is prepared again after each tap, so that the next one comes without delay. iOS itself leaves a tap out
// where there is no Taptic Engine (an iPad, an iPhone before the 7) or where the player turned the system's haptics
// off. Unity calls it on the main thread, as UIKit wants. Compiled into the iOS build only (TapticEngine.mm.meta).

#import <UIKit/UIKit.h>

extern "C" void PuzzleTapticTap(int style)
{
    static UIImpactFeedbackGenerator* generators[UIImpactFeedbackStyleHeavy + 1];
    if (style < UIImpactFeedbackStyleLight || style > UIImpactFeedbackStyleHeavy)
    {
        NSLog(@"PuzzleTapticTap: no tap of the style %d; the game asks for 0, 1 or 2.", style);
        return;
    }

    if (generators[style] == nil)
    {
        generators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:(UIImpactFeedbackStyle)style];
    }

    [generators[style] impactOccurred];
    [generators[style] prepare];
}
