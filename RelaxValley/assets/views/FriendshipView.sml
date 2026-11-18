<frame layout="1200px 800px"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="horizontal" layout="stretch stretch">

    <!-- 左侧悬停预览：头像 + 行走图 + 详细数据 -->
    <frame layout="220px stretch"
           background={@Mods/StardewUI/Sprites/ControlBorder}
           padding="16"
           margin="0,0,16,0">
      <lane orientation="vertical" layout="stretch stretch">
        <lane orientation="vertical" layout="stretch content"
              horizontal-content-alignment="middle"
              *if={HasPreview}>
          <label font="dialogue" text={PreviewName} />
          <image *if={PreviewHasPortrait}
                 layout="128px 128px"
                 sprite={PreviewPortrait}
                 margin="0,10,0,0" />
          <image *if={PreviewHasSprite}
                 layout="64px 128px"
                 sprite={PreviewSprite}
                 margin="0,8,0,0" />
          <label font="small" text={PreviewPointsText} margin="0,10,0,0" />
          <label font="small" text={PreviewLocationText} margin="0,4,0,0" />
          <label font="small" text={PreviewScheduleText} margin="0,4,0,0" />
        </lane>
        <lane orientation="vertical" layout="stretch stretch"
              horizontal-content-alignment="middle"
              vertical-content-alignment="middle"
              *!if={HasPreview}>
          <label font="small" text={PreviewHint} />
        </lane>
      </lane>
    </frame>

    <!-- 右侧列表 -->
    <lane orientation="vertical" layout="stretch stretch">
      <lane orientation="horizontal" margin="0,0,0,16" vertical-content-alignment="middle">
        <lane orientation="vertical" margin="0,0,24,0">
          <label font="dialogue" text={Title} />
          <label font="small" text={InteractionHint} margin="0,4,0,0" />
        </lane>
        <textinput layout="240px 40px"
                   placeholder={SearchPlaceholder}
                   text={<>SearchText}
                   margin="0,0,16,0" />
        <dropdown layout="240px 40px"
                  options={:SortOptions}
                  selected-index={<>SelectedSortIndex} />
      </lane>

      <scrollable layout="stretch stretch">
        <grid item-layout="length: 280" item-spacing="12,12">
          <frame *repeat={Friends}
                 layout="stretch content"
                 background={@Mods/StardewUI/Sprites/ControlBorder}
                 padding="16"
                 opacity={:CardOpacity}
                 left-click=|^OnFriendClicked(NpcName)|
                 pointer-enter=|^OnFriendHovered(NpcName)|
                 pointer-leave=|^OnFriendUnhovered()|
                 tooltip={:TooltipText}>
            <lane orientation="vertical">
              <label font="dialogue" text={:DisplayName} color={:NameColor} />
              <label font="small" text={:HeartText} color={:TextColor} margin="0,6,0,0" />
              <label font="small" text={:GiftsThisWeekText} color={:TextColor} margin="0,4,0,0" />
              <label font="small" text={:GiftStatus} color={:GiftColor} margin="0,4,0,0" />
            </lane>
          </frame>
        </grid>
      </scrollable>
    </lane>

  </lane>
</frame>
