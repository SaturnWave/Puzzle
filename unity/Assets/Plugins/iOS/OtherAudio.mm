// Plugins/iOS/OtherAudio.mm: whether another app's primary audio (the player's music, a podcast, a film) plays, so
// the game's soundtrack should be silent. Apple's hint for games under the Ambient category; never the game itself.
#import <AVFoundation/AVFoundation.h>

extern "C" int PuzzleOtherAudioShouldSilenceTheMusic(void)
{
    return [AVAudioSession sharedInstance].secondaryAudioShouldBeSilencedHint ? 1 : 0;
}
