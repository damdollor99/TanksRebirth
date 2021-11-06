﻿namespace WiiPlayTanksRemake.Enums
{
    public enum TankTier : byte
    {
        None,
        Brown,
        Ash,
        Marine,
        Yellow,
        //Bubblegum,
        Pink,
        Green,
        Purple,
        White,
        Black,
        //Marble
    }

    public enum PlayerType : byte
    {
        Blue,
        Red
    }

    public enum BulletType : byte
    {
        Regular,
        Rocket,
        RicochetRocket
    }

    public enum MenuMode : byte
    {
        MainMenu,
        PauseMenu,
        IngameMenu,
        LevelEditorMenu
    }
}