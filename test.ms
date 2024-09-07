engine = Tilengine.init(400, 240, 1, 0, 20, Tilengine.flags.vsync, "CsTilengine/samples/assets/sonic")
tilemap = engine.Tilemap.fromFile("Sonic_md_fg1.tmx")

tilemap.setLayer(0)

frame = 0

while engine.process()
    engine.drawFrame frame
    frame += 1
end while