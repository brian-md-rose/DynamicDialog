# Robot Banter

Two robots, Bolt and Sprocket, wander a room and talk about what they see.

Open `RobotBanter.unity` and press Play. Everything they say is in `RobotBanter.dlg`, and it shows off:

- **Specific beats general:** any object gets a generic remark; barrels, radiators and the glowing barrel get their own.
- **Memory:** Bolt counts barrels (`seen_barrels`), and his remarks change on the second and later sightings.
- **Conversations:** Bolt's first barrel line hands off to Sprocket, who hands back to Bolt.
- **Whoever's in earshot:** Sprocket's soda line asks `then any SodaReply`.
- **Idle chatter:** throttled with `rand`, and only when someone is nearby (`nearby_speakers`, `near_Sprocket`).
- **Interruptions:** walking near the glowing barrel makes a robot shout, cutting off its line. Replies check
  `in_danger missing`, so a conversation in progress ends on its own.

Save `RobotBanter.dlg` while playing to hear your edits straight away. Open **Window > Dynamic Dialog > Debugger**
to see every lookup and why each rule did or didn't play.

The room is built in code by `RobotBanterSetup`, so the scene holds a single GameObject.
