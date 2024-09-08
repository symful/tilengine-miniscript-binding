frame = 0
Hres = 400
Vres = 240
NumBackgroundStrips = 6
skyColor = [
	[ // top color
		27,
		0,
		139,
	],
	[
		0,
		116,
		215,
	],
] // bottom color
waterColor = [
	[ // top color
		36,
		146,
		219,
	],
	[
		31,
		127,
		190,
	],
] // bottom color
posForeground = 0
posBackground = [
	0,
	0,
	0,
	0,
	0,
	0,
]
incBackground = [
	0.562,
	0.437,
	0.375,
	0.625,
	1,
	2,
]
foreground = 0
background = 1

loadLayer = function(layer, filename)
	tilemap = engine.Tilemap.fromFile(filename)
	tilemap.setLayer layer
end function

lerp = function(x, x0, x1, fx0, fx1)
	return fx0 + (fx1 - fx0) * (x - x0) / (x1 - x0)
end function

// color interpolation 
interpolateColor = function(v, v1, v2, color1, color2)
	return [
		lerp(
			v,
			v1,
			v2,
			color1[0],
			color2[0]),
		lerp(
			v,
			v1,
			v2,
			color1[1],
			color2[1]),
		lerp(
			v,
			v1,
			v2,
			color1[2],
			color2[2]),
	]
end function

c = 0
frame = 0
speed = 0

engine = Tilengine.init(
	Hres,
	Vres,
	2,
	0,
	20,
	Tilengine.flags.vsync,
	"CsTilengine/samples/assets/sonic")

loadLayer foreground, "Sonic_md_fg1.tmx"
loadLayer background, "Sonic_md_bg1.tmx"

// load sequences for animations
sp = engine.SequencePack.fromFile("Sonic_md_seq.sqx")
waterSequence = sp.find("seq_water")
waterSequence.setPaletteAnimation 0, background, true

// setup raster callback 
callback = function(line)
	pos = -1

	if line == 1 then
		pos = posBackground[0]
	else if line == 32 then
		pos = posBackground[1]
	else if line == 48 then
		pos = posBackground[2]
	else if line == 64 then
		pos = posBackground[3]
	else if line == 112 then
		pos = posBackground[4]
	else if line >= 152 then
		pos = lerp(
			line,
			152,
			224,
			posBackground[4],
			posBackground[5])
	end if

	if pos != -1 then
		engine.setLayerPosition background, pos, 0
	end if

	// background color gradients 
	if line < 112 then
		rgb = interpolateColor(
			line,
			0,
			112,
			skyColor[0],
			skyColor[1])

		engine.setBackgroundColor rgb[0], rgb[1], rgb[2]
	else if line >= 144 then
		rgb = interpolateColor(
			line,
			144,
			Vres,
			waterColor[0],
			waterColor[1])

		engine.setBackgroundColor rgb[0], rgb[1], rgb[2]
	end if
end function
engine.setRasterCallback @callback

globals.loop = function
	if engine.getInput(Tilengine.inputs.right) then

		globals.speed += 0.02

		if speed > 1 then
			globals.speed = 1
		end if

	else if speed > 0 then

		globals.speed -= 0.02
		if speed < 0 then
			globals.speed = 0
		end if
	end if

	if engine.getInput(Tilengine.inputs.left) then

		globals.speed -= 0.02
		if speed < -1 then
			globals.speed = -1
		end if
	else if speed < 0 then
		globals.speed += 0.02
		if speed > 0 then
			globals.speed = 0
		end if
	end if

	// scroll 
	globals.posForeground += 3 * speed
	engine.setLayerPosition foreground, posForeground, 0
	c = 0
	while c < NumBackgroundStrips
		posBackground[c] = posBackground[c] + ((incBackground[c] * speed))

		c += 1
	end while

	// render to window 
	engine.drawFrame frame
	globals.frame += 1
end function