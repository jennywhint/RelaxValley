<frame layout="85% 85%"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="vertical" layout="stretch stretch">

    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle">
      <label font="dialogue" text={:Title} />
    </lane>
    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle" margin="0,4,0,18">
      <label font="small" text={:Hint} color="#7A5230" />
    </lane>

    <lane *!if={HasFlowers} orientation="vertical" layout="stretch stretch"
          horizontal-content-alignment="middle" vertical-content-alignment="middle">
      <label font="dialogue" text={:EmptyText} color="#8A8580" />
    </lane>

    <scrollable *if={HasFlowers} layout="stretch stretch">
      <grid item-layout="length: 280" item-spacing="12,12">
        <frame *repeat={:Flowers}
               layout="250px 96px"
               background={@Mods/StardewUI/Sprites/ControlBorder}
               padding="12"
               left-click=|^OnFlowerClicked(Id)|>
          <lane orientation="horizontal" layout="stretch stretch" vertical-content-alignment="middle">
            <image *if={:HasSprite}
                   layout="64px 64px"
                   sprite={:Sprite}
                   margin="0,0,12,0" />
            <lane orientation="vertical" layout="stretch content" vertical-content-alignment="middle">
              <label font="dialogue" text={:Name} color="#3B2A1E" />
              <label font="small" text={:CountText} color="#B06A00" margin="0,4,0,0" />
            </lane>
          </lane>
        </frame>
      </grid>
    </scrollable>

  </lane>
</frame>
