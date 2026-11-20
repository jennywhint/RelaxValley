<frame layout="72% 78%"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="vertical" layout="stretch stretch">

    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle">
      <label font="dialogue" text={:Title} />
    </lane>
    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle" margin="0,4,0,20">
      <label font="small" text={:Hint} color="#7A5230" />
    </lane>

    <scrollable layout="stretch stretch">
      <grid item-layout="length: 150" item-spacing="14,14">
        <frame *repeat={:Levels}
               layout="150px 68px"
               background={@Mods/StardewUI/Sprites/ControlBorder}
               padding="10"
               left-click=|^OnLevelClicked(Value)|>
          <lane orientation="vertical" layout="stretch stretch"
                horizontal-content-alignment="middle"
                vertical-content-alignment="middle">
            <label font="dialogue" text={:Text} color="#3B2A1E" />
          </lane>
        </frame>
      </grid>
    </scrollable>

  </lane>
</frame>
