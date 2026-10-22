// The first of the player's preferred languages as a BCP 47 tag ("pt-BR", "zh-Hant-HK"), for PhoneLanguage.cs
// (Scripts/Engine), as Android's Locale.getDefault() gives it there; empty where the iPhone lists none. The string is
// a copy made with malloc, which the C# side's marshaller frees. Compiled into the iOS build only (PhoneLanguage.mm.meta).

#import <Foundation/Foundation.h>
#include <string.h>

extern "C" char* PuzzlePreferredLanguageTag(void)
{
    NSString* tag = [[NSLocale preferredLanguages] firstObject];
    return strdup(tag != nil ? [tag UTF8String] : "");
}
