namespace Puzzle.Museum
{
    /// <summary>
    /// The museum's fixed measures (docs/GAME_DESIGN.md section 18). They follow the level generator's own cycles: a
    /// painting is one saw-tooth of difficulty, a room is one palette chapter, so the hardest level of every ten
    /// gives a painting's last pieces and every fifty levels finish a room.
    /// </summary>
    public static class MuseumPlan
    {
        public const int LevelsPerPainting = 10;
        public const int PaintingsPerRoom = 5;
        public const int RoomCount = 20;

        public const int LevelsPerRoom = LevelsPerPainting * PaintingsPerRoom;
        public const int PaintingCount = RoomCount * PaintingsPerRoom;
        public const int LevelCount = PaintingCount * LevelsPerPainting;
    }
}
