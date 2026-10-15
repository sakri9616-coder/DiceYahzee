"""Three hand-composed UI studies using the attached DiceYahzee screenshot's dice.

No generative image model or external art is used. Run with Pillow.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "design" / "ui-concepts"
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = Image.open(OUT / "source-screenshot.png").convert("RGB")
W, H, S = 1424, 714, 2
JP = r"C:\Windows\Fonts\NotoSansJP-VF.ttf"
SERIF = r"C:\Windows\Fonts\georgia.ttf"
SANS = r"C:\Windows\Fonts\segoeui.ttf"
PIPS = [2, 5, 2, 1, 4]
ENEMY = [3, 6, 6, 3, 4]
ROLES = ["ペア", "ツーペア", "スリーカインド", "フルハウス", "小ストレート", "フォーカインド", "大ストレート", "ヤッツィー", "チャンス"]
VALS = ["43", "35", "61", "35", "35", "35", "35", "35", "35"]
MULTS = ["1.25", "1.5", "1.75", "2", "2", "2.5", "2.5", "3.25", "1"]
RAW_DICE = [SOURCE.crop(box) for box in [
    (394, 284, 506, 396), (526, 284, 638, 396), (658, 284, 770, 396),
    (790, 284, 902, 396), (921, 284, 1033, 396),
]]


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


class Board:
    def __init__(self, bg):
        self.im = Image.new("RGBA", (W * S, H * S), rgb(bg) + (255,))
        self.d = ImageDraw.Draw(self.im)

    def rect(self, xy, fill, outline=None, width=1, radius=0):
        xy = tuple(int(v * S) for v in xy)
        kwargs = {"fill": rgb(fill) if isinstance(fill, str) else fill}
        if outline:
            kwargs.update(outline=rgb(outline), width=width * S)
        if radius:
            self.d.rounded_rectangle(xy, radius * S, **kwargs)
        else:
            self.d.rectangle(xy, **kwargs)

    def line(self, pts, fill, width=1):
        self.d.line([(int(x * S), int(y * S)) for x, y in pts], fill=rgb(fill), width=width * S, joint="curve")

    def ellipse(self, xy, fill, outline=None, width=1):
        kw = {"fill": rgb(fill)}
        if outline:
            kw.update(outline=rgb(outline), width=width * S)
        self.d.ellipse(tuple(int(v * S) for v in xy), **kw)

    def text(self, x, y, value, size, color, anchor="lt", font=JP):
        f = ImageFont.truetype(font, int(size * S))
        self.d.text((int(x * S), int(y * S)), value, fill=rgb(color), font=f, anchor=anchor,
                    stroke_width=0)

    def die(self, index, x, y, size=108, border="#D8CDB5", shadow="#070B0E"):
        self.rect((x + 5, y + 7, x + size + 5, y + size + 7), shadow, radius=9)
        face = RAW_DICE[index].resize((size * S, size * S), Image.Resampling.LANCZOS).convert("RGBA")
        mask = Image.new("L", face.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, size * S - 1, size * S - 1), radius=6 * S, fill=255)
        self.im.paste(face, (x * S, y * S), mask)
        self.rect((x, y, x + size - 1, y + size - 1), None, border, 2, 6)

    def small_die(self, value, x, y, size=40, face="#EAE4D6", pip="#1A2330", border="#BBAF97"):
        self.rect((x + 2, y + 3, x + size + 2, y + size + 3), "#11161A", radius=4)
        self.rect((x, y, x + size, y + size), face, border, 1, 4)
        c = x + size / 2
        cy = y + size / 2
        o = size * .255
        p = [(c-o,cy-o),(c+o,cy-o),(c-o,cy),(c,cy),(c+o,cy),(c-o,cy+o),(c+o,cy+o)]
        mapping = {1:[3],2:[0,6],3:[0,3,6],4:[0,1,5,6],5:[0,1,3,5,6],6:[0,1,2,4,5,6]}
        r = max(2.4, size * .061)
        for ix in mapping[value]:
            px, py = p[ix]
            self.ellipse((px-r,py-r,px+r,py+r), pip)

    def hp(self, x, y, w, value, color, track, label, text_color, slim=False):
        h = 18 if slim else 22
        self.rect((x, y, x+w, y+h), track, radius=h//2)
        self.rect((x, y, x+w*value, y+h), color, radius=h//2)
        self.text(x+w/2, y+h/2-1, label, 14, text_color, "mm")

    def save(self, x, y, w, h, fill, fg, border, subtitle=None):
        self.rect((x, y+4, x+w, y+h+4), "#0A0E10", radius=7)
        self.rect((x, y, x+w, y+h), fill, border, 1, 7)
        self.text(x+30, y+h/2-1, "持越", 19, fg, "lm")
        self.text(x+w-20, y+h/2-1, "1", 17, fg, "rm", SANS)
        self.line([(x+14,y+h/2-7),(x+14,y+h/2+7),(x+25,y+h/2)],fg,2)
        if subtitle:
            self.text(x+w/2, y+h+17, subtitle, 11, fg, "mm")

    def guide(self, x, y, color):
        # Down-only motion guide, a mouse outline followed by three chevrons.
        self.rect((x-11,y-12,x+11,y+15), None, color, 2, 8)
        self.line([(x,y-11),(x,y-2)], color, 2)
        self.ellipse((x-2,y-7,x+2,y-3), color)
        for k in range(3):
            yy=y+36+k*17
            self.line([(x-10,yy-5),(x,yy+4),(x+10,yy-5)],color,2)

    def pips(self, x, y, color):
        self.ellipse((x-17,y-4,x-9,y+4),color)
        self.ellipse((x+9,y-4,x+17,y+4),color)

    def output(self, name):
        path = OUT / name
        self.im.convert("RGB").resize((W,H), Image.Resampling.LANCZOS).save(path, quality=95)
        return path


def role_card(b, x, y, w, light=True, own=False):
    ink = "#26343B" if light else "#D8DFDB"
    muted = "#69746F" if light else "#879A9A"
    line = "#D6D1C3" if light else "#344B50"
    face = "#F0EADF" if light else "#15282D"
    active = "#E7D39C" if light else "#B99855"
    b.rect((x,y,x+w,y+342),face, "#B8AE96" if light else "#4B6869",1,5)
    b.text(x+18,y+23,"役" if own else "相手の役",17,ink,"lm")
    if own:
        b.text(x+w-17,y+23,"◉",16,muted,"rm")
    b.line([(x+16,y+43),(x+w-16,y+43)],line,1)
    for i, role in enumerate(ROLES):
        yy=y+48+i*31
        if own and i==2:
            b.rect((x+8,yy,x+w-8,yy+29),active,radius=3)
            b.rect((x+8,yy,x+11,yy+29),"#9A7839")
        b.text(x+18,yy+15,role,14,ink if (own and i==2) or light else muted,"lm")
        if own:
            b.text(x+w-49,yy+15,"×"+MULTS[i],10,muted,"rm")
            b.text(x+w-17,yy+15,VALS[i],16,ink,"rm",SANS)
        else:
            b.text(x+w-18,yy+15,"—",17,muted,"rm",SANS)
        if i<8:
            b.line([(x+17,yy+30),(x+w-17,yy+30)],line)


def variant_one():
    b=Board("#10191C")
    # Calm editorial layout: each band has one job.
    b.rect((0,0,W,70),"#0B1113")
    b.text(74,34,"DiceYahzee",26,"#D7B46C","lm",SERIF)
    b.text(712,34,"ROUND 01",13,"#B6C2BF","mm",SANS)
    b.text(1337,34,"遊び方",15,"#B6C2BF","rm")
    b.line([(73,69),(1351,69)],"#344248")
    b.rect((318,83,1106,632),"#1B2B30","#526267",1,13)
    b.rect((326,91,1098,624),"#20333A",radius=8)
    role_card(b,83,192,222,True,False)
    role_card(b,1119,192,222,True,True)
    b.text(712,111,"相手の手",14,"#C4C8C0","mm")
    b.hp(485,130,454,1,"#BE7470","#415055","相手   240 / 240","#19272B",True)
    for i,n in enumerate(ENEMY): b.small_die(n,587+i*52,166,43)
    b.text(712,231,"ツーペア  ·  42 × 1.5",13,"#9FAFAA","mm")
    b.text(712,257,"63",35,"#E0B970","mm",SERIF)
    b.line([(383,279),(1041,279)],"#466067")
    for i in range(5): b.die(i,410+i*123,314,105)
    b.guide(1062,340,"#87C9B5")
    b.text(712,459,"スリーカインド",21,"#E7ECE8","mm")
    b.text(712,495,"35 × 1.75",14,"#8FA7A4","mm")
    b.text(712,534,"61",49,"#DAB56D","mm",SERIF)
    b.save(616,559,192,51,"#D4AF68","#203036","#EAD6A8")
    b.hp(396,641,632,1,"#75B6A6","#43585A","あなた   240 / 240","#10242A")
    b.pips(712,682,"#D2AE61")
    return b.output("01_editorial_classic.png")


def variant_two():
    b=Board("#0C1519")
    # Broadcast arena: dark open field and typographic score rails.
    b.rect((0,0,W,4),"#B89355")
    b.text(62,43,"DICEYAHZEE",21,"#ECF1EA","lm",SANS)
    b.text(712,43,"01  /  09",13,"#8FA5A4","mm",SANS)
    b.rect((1285,21,1362,56),"#223137",radius=4)
    b.text(1323,38,"遊び方",13,"#C5D0C9","mm")
    b.line([(310,75),(1114,75)],"#3F565B")
    b.text(712,103,"相手",13,"#B9C8C4","mm")
    b.hp(520,116,384,1,"#C87470","#2A4045","240 / 240","#19272B",True)
    for i,n in enumerate(ENEMY): b.small_die(n,595+i*49,157,40,"#EAE8DF","#16232B","#AEB8B0")
    b.text(712,217,"ツーペア",14,"#B4C0BC","mm")
    b.text(712,255,"63",36,"#E6CC8E","mm",SERIF)
    # Typographic rails, without overlay windows.
    b.text(76,146,"OPPONENT",12,"#789493","lm",SANS)
    b.text(76,177,"使った役",20,"#E4E8DF","lm")
    b.line([(76,194),(281,194)],"#4C6267")
    b.text(1347,146,"YOUR HAND",12,"#789493","rm",SANS)
    b.text(1347,177,"役を選ぶ",20,"#E4E8DF","rm")
    b.line([(1143,194),(1347,194)],"#4C6267")
    for i,role in enumerate(ROLES):
        yy=215+i*38
        b.text(77,yy,role,14,"#8BA09D","lt")
        b.text(276,yy,"—",17,"#5F7475","rt",SANS)
        if i==2:
            b.rect((1137,yy-7,1353,yy+28),"#B9995E",radius=3)
            b.rect((1137,yy-7,1141,yy+28),"#EAD6A8")
        b.text(1149,yy,role,14,"#12252A" if i==2 else "#BBCBC6","lt")
        b.text(1342,yy,VALS[i],17,"#14282B" if i==2 else "#E6E8DD","rt",SANS)
        if i<8:
            b.line([(77,yy+30),(276,yy+30)],"#273B40")
            b.line([(1149,yy+30),(1347,yy+30)],"#273B40")
    # The five dice are the unbroken focal point.
    b.line([(370,285),(1054,285)],"#445B60")
    b.text(712,307,"この手",13,"#8DA9A3","mm")
    for i in range(5): b.die(i,420+i*119,332,103,"#BFCBC4")
    b.guide(1065,357,"#7ED4BD")
    b.text(712,451,"スリーカインド",21,"#E9EFEB","mm")
    b.text(712,489,"35  ×  1.75",14,"#A0ADA8","mm")
    b.text(712,535,"61",60,"#E2C485","mm",SERIF)
    b.save(618,563,188,48,"#D4B06E","#1A2C31","#E6D2A4")
    b.hp(426,643,572,1,"#6FBBA9","#2B4547","あなた   240 / 240","#102328")
    b.pips(712,684,"#E0BA70")
    return b.output("02_broadcast_arena.png")


def variant_three():
    b=Board("#E8E3D8")
    # Graphic board: parchment surrounds one strong, uninterrupted felt field.
    b.rect((0,0,W,76),"#EDE9DE")
    b.text(76,40,"DiceYahzee",28,"#25363B","lm",SERIF)
    b.text(712,40,"ROUND 01",13,"#73817D","mm",SANS)
    b.text(1350,40,"遊び方",15,"#4E6060","rm")
    b.line([(66,74),(1358,74)],"#C8C0B0")
    b.rect((338,89,1086,625),"#193137","#A99A7D",2,13)
    b.rect((348,99,1076,615),"#244047",radius=8)
    b.text(712,118,"相手  240 / 240",13,"#E6E4D8","mm")
    b.hp(511,135,402,1,"#C58579","#50646A","", "#FFFFFF",True)
    for i,n in enumerate(ENEMY): b.small_die(n,595+i*49,169,40,"#EDE9DC","#1A2931","#CFBF9A")
    b.text(712,236,"ツーペア  /  42 × 1.5",13,"#AFC5C0","mm")
    b.text(712,265,"63",33,"#E2BE74","mm",SERIF)
    b.rect((377,290,1047,435),"#152B31","#4F6B6D",1,10)
    for i in range(5): b.die(i,410+i*123,310,105,"#C5A96A")
    b.guide(1060,326,"#E2BF77")
    b.text(712,457,"スリーカインド",19,"#E4EADD","mm")
    b.text(712,474,"35 × 1.75",13,"#B8CDC4","mm")
    b.text(712,517,"61",55,"#F0D28C","mm",SERIF)
    b.save(620,551,184,46,"#E3C27D","#1D3338","#F1DFAE")
    b.hp(445,607,534,1,"#8BC9B3","#4E6868","あなた   240 / 240","#163036",True)
    b.pips(712,651,"#B98F4D")
    # Role lists are directly typeset on the light surround, with no panel boxes.
    b.text(81,126,"相手の役",18,"#283C40","lm")
    b.line([(81,150),(313,150)],"#AB9F87")
    b.text(1112,126,"役",18,"#283C40","lm")
    b.text(1341,126,"↕",17,"#876D43","rm")
    b.line([(1112,150),(1343,150)],"#AB9F87")
    for i,role in enumerate(ROLES):
        yy=174+i*43
        if i==2:
            b.rect((1103,yy-7,1350,yy+30),"#D5BD80",radius=3)
            b.rect((1103,yy-7,1107,yy+30),"#7F653D")
        b.text(83,yy,role,14,"#54615E","lt")
        b.text(309,yy,"—",16,"#8C938A","rt",SANS)
        b.text(1115,yy,role,14,"#24363A","lt")
        b.text(1340,yy,VALS[i],17,"#24363A","rt",SANS)
        if i<8:
            b.line([(83,yy+29),(309,yy+29)],"#CEC6B6")
            b.line([(1115,yy+29),(1340,yy+29)],"#CEC6B6")
    b.text(77,670,"THROW  /  HOLD  /  SCORE",11,"#8A9189","lt",SANS)
    return b.output("03_graphic_table.png")


if __name__ == "__main__":
    for build in (variant_one,variant_two,variant_three):
        print(build())
