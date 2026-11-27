using System;
using MobileQuestVR.Environment;
using MobileQuestVR.Localization;
using MobileQuestVR.Settings;
using NUnit.Framework;

namespace MobileQuestVR.Tests.EditMode
{
    public sealed class CoreArchitectureEditModeTests
    {
        [Test]
        public void Localization_SwitchesCleanlyBetweenSpanishAndEnglish()
        {
            LocalizationService.SetLanguage(AppLanguage.Spanish);
            Assert.AreEqual("Dormitorio Futurista", LocalizationService.Get("room.futuristic_bedroom"));

            LocalizationService.SetLanguage(AppLanguage.English);
            Assert.AreEqual("Futuristic Bedroom", LocalizationService.Get("room.futuristic_bedroom"));

            LocalizationService.SetLanguage(AppLanguage.Spanish);
        }

        [Test]
        public void RoomId_ContainsAllSevenRequired3DRooms()
        {
            RoomId[] rooms = (RoomId[])Enum.GetValues(typeof(RoomId));
            Assert.AreEqual(7, rooms.Length);
            foreach (RoomId r in rooms)
            {
                string key = RoomManager.GetRoomLocalizationKey(r);
                Assert.IsFalse(string.IsNullOrEmpty(LocalizationService.Get(key)));
            }
        }

        [Test]
        public void Preferences_DefaultsToNoStartupPinAnd63mmIPD()
        {
            var data = new OSPreferencesData();
            Assert.IsFalse(data.ProtectSensitiveSettingsWithPin);
            Assert.AreEqual(63.0f, data.InterpupillaryDistanceMm, 0.01f);
            Assert.AreEqual(RoomId.FuturisticBedroom, data.ActiveRoom);
        }
    }
}
