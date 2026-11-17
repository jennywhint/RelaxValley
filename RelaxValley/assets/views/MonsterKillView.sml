<frame layout="1200px 800px"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="vertical">

    <label font="dialogue" text={Title} margin="0,0,0,16" />

    <scrollable>
      <grid item-layout="length: 280" item-spacing="12,12">
        <frame *repeat={Goals}
               layout="stretch content"
               background={@Mods/StardewUI/Sprites/ControlBorder}
               padding="16">
          <lane orientation="vertical">
            <label font="dialogue" text={:DisplayName} />
            <label font="small" text={:ProgressText} color={:TextColor} margin="0,6,0,0" />
          </lane>
        </frame>
      </grid>
    </scrollable>

  </lane>
</frame>