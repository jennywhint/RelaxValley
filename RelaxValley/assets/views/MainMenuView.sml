<frame layout="92% 90%"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36,36,40,36"
       padding="32,24">
  <lane orientation="vertical" layout="stretch stretch">

    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle">
      <label font="dialogue" text={Title} />
    </lane>
    <lane orientation="horizontal" layout="stretch content" horizontal-content-alignment="middle" margin="0,4,0,10">
      <label font="small" text={Hint} color="#7A5230" />
    </lane>

    <lane *if={ShowBack}
          orientation="horizontal"
          layout="stretch content"
          horizontal-content-alignment="middle"
          margin="0,0,0,10">
      <frame layout="content content"
             background={@Mods/StardewUI/Sprites/ControlBorder}
             padding="18,11"
             left-click=|GoBack()|>
        <label font="dialogue" text={:BackText} color="#7A4A20" />
      </frame>
    </lane>

    <scrollable layout="stretch stretch">
      <lane orientation="vertical" layout="stretch content">

        <lane *repeat={Sections} orientation="vertical" layout="stretch content">

          <lane *if={:HasHeader}
                layout="stretch content"
                horizontal-content-alignment="middle"
                margin="0,10,0,6">
            <label font="dialogue" text={:Header} color="#7A4A20" />
          </lane>

          <grid item-layout="length: 300" item-spacing="10,10">
            <frame *repeat={:Entries}
                   layout="stretch content"
                   background={@Mods/StardewUI/Sprites/ControlBorder}
                   padding="14,13"
                   opacity={:Opacity}
                   left-click=|Click()|
                   right-click=|RightClick()|>
              <label font="dialogue" text={:Text} color={:TextColor} />
            </frame>
          </grid>

        </lane>

      </lane>
    </scrollable>

  </lane>
</frame>
