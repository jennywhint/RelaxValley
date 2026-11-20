<frame layout="62% 72%"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="vertical" layout="stretch stretch">

    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle">
      <label font="dialogue" text={:Title} />
    </lane>
    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle" margin="0,6,0,10">
      <label font="dialogue" text={:Summary} color="#3B2A1E" />
    </lane>
    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle" margin="0,0,0,14">
      <label font="small" text={:ProgressText} color="#B06A00" />
    </lane>

    <lane orientation="vertical" layout="stretch content">
      <lane *repeat={:Details} orientation="horizontal" layout="stretch content"
            horizontal-content-alignment="middle" margin="0,2,0,2">
        <label font="small" text={:Text} color="#5A4A3A" />
      </lane>
    </lane>

    <lane orientation="horizontal" layout="stretch stretch" vertical-content-alignment="bottom"
          horizontal-content-alignment="middle">
      <label font="small" text={:Hint} color="#8A8580" />
    </lane>

  </lane>
</frame>
