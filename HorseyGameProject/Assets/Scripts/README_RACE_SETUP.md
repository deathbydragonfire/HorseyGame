# Race AI Setup (RaceScene2)

## Where HorseRacerAI goes

**HorseRacerAI must be on the Opponent GameObject** (the root of the horse that races the player—the one named "Opponent" in the hierarchy). If you don’t see it there, add it manually (see below).

---

## Manual setup (if components are missing)

1. **Opponent** (the horse that should be AI-driven):
   - In the Hierarchy, select **Opponent**.
   - **Add Component** → search for **Horse Racer AI** (script: `HorseRacerAI`).
   - In the Inspector, set **Path Link** to the **Raceline** GameObject (the one that has the spline). If Raceline has a **PathLink_Spline** component, assign that same GameObject; the script will use its PathLink_Spline.
   - Add **Racer Id** (script: `RacerId`). Set **Racer** to **Opponent**.
   - **Disable** the **Malbers Input** (or **M Input**) component on Opponent so the AI controls the horse. (HorseRacerAI can also disable it at runtime if "Disable Input When Active" is checked.)

2. **Raceline** (the spline that traces the course):
   - Select **Raceline**.
   - If it doesn’t have **Path Link (Spline)**, add it: **Add Component** → search for **Path Link** or **PathLink_Spline** (Malbers).
   - Set **Spline** to Raceline’s own **Spline Container** (drag the Raceline object; the Spline Container is on it).

3. **Player** (for finish-line detection):
   - Select **Player**.
   - Add **Racer Id** and set **Racer** to **Player**.

4. **RaceManager**:
   - If there’s no **RaceManager** in the scene: create an empty GameObject, name it **RaceManager**, add the **Race Manager** script.
   - Assign **Player** and **Opponent** (drag the Player and Opponent roots from the hierarchy).
   - Optionally assign **Path For Progress** to Raceline’s **PathLink_Spline** (on Raceline) for rubber-banding.

5. **Finish/Start** (finish line trigger):
   - Select **Finish/Start**.
   - Add **Finish Line Trigger** (script: `FinishLineTrigger`). Leave **Race Manager** empty to use the one in the scene automatically.

---

## Recommended: HorseRacerAI on RaceManager

To avoid the Opponent moving with your keyboard, put **HorseRacerAI on the RaceManager** (not on the Opponent):

1. Select **RaceManager** in the scene.
2. **Add Component** → **Horse Racer AI**.
3. Set **Racer** to the **Opponent** GameObject (drag Opponent from the hierarchy).
4. Set **Path Link** to the **Raceline** GameObject (the one with the spline).
5. Leave **Disable Input When Active** checked.

**RaceManager** already blocks the Opponent’s input every frame when **Opponent** is assigned, so the Opponent is driven only by the AI. Make sure **RaceManager** has **Player** and **Opponent** assigned in the Inspector.

## Quick checklist

| GameObject   | Components |
|-------------|------------|
| **RaceManager** | Race Manager (Player & Opponent assigned). **Horse Racer AI** with **Racer** = Opponent, **Path Link** = Raceline. |
| **Opponent** | RacerId (Opponent). No need to put HorseRacerAI here if it’s on RaceManager. |
| **Raceline** | Spline Container, PathLink_Spline (Spline = its Spline Container). |
| **Player**   | RacerId (Player). |
| **Finish/Start** | Box Collider (trigger), FinishLineTrigger. |

Laps to win are set on RaceManager (default 1). Use **On Race Finished** event for UI or next scene.
