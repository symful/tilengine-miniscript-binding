frame = 0
engine = Tilengine.init(400, 240, 1, 0, 20, Tilengine.flags.vsync, "CsTilengine/samples/assets/sonic")
tilemap = engine.Tilemap.fromFile("Sonic_md_fg1.tmx")

tilemap.setLayer(0)

globals.loop = function()
    engine.drawFrame globals.frame
    globals.frame += 1
end function