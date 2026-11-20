<frame layout="88% 88%"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="vertical" layout="stretch stretch">

    <!-- 标题 + 搜索 + 筛选 -->
    <lane orientation="vertical" margin="0,0,0,12">
      <lane orientation="horizontal" vertical-content-alignment="middle">
        <lane orientation="vertical" margin="0,0,24,0">
          <label font="dialogue" text={Title} />
          <label font="small" text={Hint} margin="0,4,0,0" />
        </lane>
        <textinput layout="220px 40px"
                   placeholder={SearchPlaceholder}
                   text={<>SearchText}
                   margin="0,0,12,0" />
        <lane layout="content content" margin="0,0,12,0">
          <dropdown layout="200px 40px"
                    options={:SourceOptions}
                    selected-index={<>SelectedSourceIndex} />
        </lane>
        <lane layout="content content" margin="0,0,12,0">
          <dropdown layout="180px 40px"
                    options={:TypeOptions}
                    selected-index={<>SelectedTypeIndex} />
        </lane>
        <lane layout="content content" margin="0,0,12,0">
          <dropdown layout="140px 40px"
                    options={:StatusOptions}
                    selected-index={<>SelectedStatusIndex} />
        </lane>
        <label font="small" text={CountText} />
      </lane>
    </lane>

    <!-- 任务卡片列表 -->
    <scrollable layout="stretch stretch">
      <grid item-layout="length: 360" item-spacing="12,12">
        <frame *repeat={Quests}
               layout="stretch content"
               background={@Mods/StardewUI/Sprites/ControlBorder}
               padding="16"
               tooltip={:Description}>
          <lane orientation="vertical">
            <label font="dialogue" text={:Title} />
            <lane orientation="horizontal" margin="0,6,0,0">
              <label font="small" text={:SourceLabel} color="#7A5230" />
              <label font="small" text={:TypeLabel} color="#7A5230" margin="12,0,0,0" />
              <label font="small" text={:StatusText} color={:StatusColor} margin="12,0,0,0" />
            </lane>
            <label font="small" text={:Objective} margin="0,6,0,0" />
            <label font="small" text={:RewardText} color="#2E7D32" margin="0,4,0,0" />
            <lane *if={:CanAccept} orientation="horizontal" margin="0,8,0,0">
              <frame layout="content content"
                     background={@Mods/StardewUI/Sprites/ControlBorder}
                     padding="12,4"
                     left-click=|^AcceptQuest(Id)|>
                <label font="small" text={:AcceptText} />
              </frame>
            </lane>
          </lane>
        </frame>
      </grid>
    </scrollable>

  </lane>
</frame>
