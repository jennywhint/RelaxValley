<frame layout="1200px 800px"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="vertical">

    <lane orientation="horizontal" margin="0,0,0,16" vertical-content-alignment="middle">
      <label font="dialogue" text={Title} margin="0,0,24,0" />
      <textinput layout="240px 40px"
           placeholder="搜索村民名字"
           text={<>SearchText}
           margin="0,0,16,0" />

      <dropdown layout="240px 40px"
          options={:SortOptions}
          selected-index={<>SelectedSortIndex} />
    </lane>

    <scrollable>
      <grid item-layout="length: 280" item-spacing="12,12">
        <frame *repeat={Friends}
               layout="stretch content"
               background={@Mods/StardewUI/Sprites/ControlBorder}
               padding="16">
          <lane orientation="vertical">
            <label font="dialogue" text={:DisplayName} />
            <label font="small" text={:HeartText} margin="0,6,0,0" />
            <label font="small" text={:GiftsThisWeekText} margin="0,4,0,0" />
            <label font="small" text={:GiftStatus} color={:GiftColor} margin="0,4,0,0" />
          </lane>
        </frame>
      </grid>
    </scrollable>

  </lane>
</frame>