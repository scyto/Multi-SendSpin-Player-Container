const jscad = require('@jscad/modeling')
const { cuboid, cylinder } = jscad.primitives
const { subtract, union } = jscad.booleans
const { translate, rotate, center } = jscad.transforms

const getParameterDefinitions = () => {
  return [
    { name: 'renderPart', type: 'choice', values: ['Base', 'Lid', 'Clamp_Relay', 'Clamp_DC', 'All_Preview'], initial: 'Base', caption: 'Part to Export:' },
    { name: 'wall', type: 'float', initial: 2.5, caption: 'Wall Thickness:' },
    { name: 'relayWireDia', type: 'float', initial: 2.8, caption: 'Relay Grip Dia (mm):' },
    { name: 'relayHoleDia', type: 'float', initial: 3.8, caption: 'Relay Wall Hole Dia (mm):' },
    { name: 'dcWireDia', type: 'float', initial: 3.0, caption: 'DC Wire/Groove Dia (mm):' },
    { name: 'lidLugSize', type: 'float', initial: 0.8, caption: 'Lid Lug Stick-out (mm):' }
  ]
}

const main = (params) => {
  const { renderPart, wall, relayWireDia, relayHoleDia, dcWireDia, lidLugSize } = params

  // FIXED DIMENSIONS
  const pcbL = 105.92
  const pcbW = 78.49
  const mountL = 97.41
  const mountW = 72.00

  const height = 30
  const gapRelay = 12.7
  const gapSide = 2.0
  const standoffH = 6

  const innerL = pcbL + (gapRelay * 2)
  const innerW = pcbW + (gapSide * 2)
  const outerL = innerL + (wall * 2)
  const outerW = innerW + (wall * 2)
  const outerH = height + wall

  const porchW = 12
  const porchH_Relay = wall + relayWireDia + 2
  const dcExitZ = wall + standoffH + 1
  const porchH_DC = dcExitZ

  // PORT ALIGNMENT
  const usbCenterOffset = 3.0
  const dcCenterOffset = 3.1
  const dcPortWidth = 12.0

  // RELAY POSITIONS
  const relayOffsets = [ -22.86, -7.62, 7.62, 22.86 ];
  const screwOffsets = [ -28.0, 0, 28.0 ];

  const makeBox = (w, l, h, x, y, z) => translate([x + w/2, y + l/2, z + h/2], cuboid({ size: [w, l, h] }))

  const smoothCyl = (h, r, axis = 'z') => {
    let cyl = cylinder({height: h, radius: r, segments: 128})
    if (axis === 'x') return rotate([0, Math.PI/2, 0], cyl)
    if (axis === 'y') return rotate([Math.PI/2, 0, 0], cyl)
    return cyl
  }

  const hexCutout = (x, y, z, size, rot) => {
    return translate([x, y, z], rotate(rot, cylinder({height: 40, radius: size, segments: 6})))
  }

  const createBase = () => {
    let shell = makeBox(outerL, outerW, outerH, 0, 0, 0)

    // Porches
    let porchLeft = makeBox(porchW, outerW, porchH_Relay, -porchW, 0, 0)
    let porchRight = makeBox(porchW, outerW, porchH_Relay, outerL, 0, 0)
    let porchPower = makeBox(32, porchW, porchH_DC, outerL/2 - 16 + dcCenterOffset, outerW, 0)

    let baseShell = union(shell, porchLeft, porchRight, porchPower)

    let cavity = makeBox(innerL, innerW, height + 20, wall, wall, wall)

    // USB Cutout (Updated Height and Z-Position)
    const usbZ = (wall + standoffH + 2) - 1.0
    const usbH = 13.0
    let usb = makeBox(20, wall + 10, usbH, outerL/2 - 10 + usbCenterOffset, -5, usbZ)

    let dcExit = makeBox(dcPortWidth, wall + 10, 14, outerL/2 - (dcPortWidth/2) + dcCenterOffset, outerW - wall - 5, dcExitZ)

    // PILOT HOLES
    const createBasePilot = (x, y, h) => translate([x, y, h - 5], cylinder({height: 12, radius: 1.2, segments: 32}))
    let clampPilotHoles = []

    screwOffsets.forEach(off => {
        clampPilotHoles.push(createBasePilot(-porchW/2, outerW/2 + off, porchH_Relay))
        clampPilotHoles.push(createBasePilot(outerL + porchW/2, outerW/2 + off, porchH_Relay))
    })

    clampPilotHoles.push(createBasePilot(outerL/2 - 12 + dcCenterOffset, outerW + porchW/2, porchH_DC))
    clampPilotHoles.push(createBasePilot(outerL/2 + 12 + dcCenterOffset, outerW + porchW/2, porchH_DC))

    let extraCuts = []

    relayOffsets.forEach(offset => {
        const yPos = outerW/2 + offset;
        extraCuts.push(translate([-porchW/2, yPos, porchH_Relay], smoothCyl(porchW+1, relayWireDia/2, 'x')))
        extraCuts.push(translate([wall/2, yPos, porchH_Relay], smoothCyl(wall+1, relayHoleDia/2, 'x')))
        extraCuts.push(translate([outerL + porchW/2, yPos, porchH_Relay], smoothCyl(porchW+1, relayWireDia/2, 'x')))
        extraCuts.push(translate([outerL - wall/2, yPos, porchH_Relay], smoothCyl(wall+1, relayHoleDia/2, 'x')))
    })

    const dcHoleSpacing = 3.0
    const cutLen = porchW + wall + 5
    extraCuts.push(translate([outerL/2 - dcHoleSpacing + dcCenterOffset, outerW + porchW/2 - (cutLen/2) + (porchW/2), porchH_DC], smoothCyl(cutLen, dcWireDia/2, 'y')))
    extraCuts.push(translate([outerL/2 + dcHoleSpacing + dcCenterOffset, outerW + porchW/2 - (cutLen/2) + (porchW/2), porchH_DC], smoothCyl(cutLen, dcWireDia/2, 'y')))

    for(let x = 18; x < outerL - 18; x += 8.5) {
      for(let z = 12; z < outerH - 5; z += 6.5) {
         let rowOff = (Math.round(z/6.5) % 2 === 0) ? 0 : 4.25
         let curX = x + rowOff
         let distToUSB = Math.sqrt(Math.pow(outerL/2 + usbCenterOffset - curX, 2) + Math.pow(wall + standoffH + 8 - z, 2))
         let distToDC = Math.sqrt(Math.pow(outerL/2 + dcCenterOffset - curX, 2) + Math.pow(wall + standoffH + 8 - z, 2))

         if (distToUSB < 18) continue
         extraCuts.push(hexCutout(curX, -5, z, 2.8, [Math.PI/2, 0, 0]))
         if (distToDC > 18) {
            extraCuts.push(hexCutout(curX, outerW + 5, z, 2.8, [Math.PI/2, 0, 0]))
         }
      }
    }

    let baseFinal = subtract(baseShell, cavity, usb, dcExit, ...clampPilotHoles, ...extraCuts)

    const sPos = [
      [outerL/2 - (mountL/2), outerW/2 - (mountW/2)], [outerL/2 + (mountL/2), outerW/2 - (mountW/2)],
      [outerL/2 - (mountL/2), outerW/2 + (mountW/2)], [outerL/2 + (mountL/2), outerW/2 + (mountW/2)]
    ]
    let standoffs = sPos.map(p => {
        let post = translate([p[0], p[1], wall], cylinder({height: standoffH, radius: 3.5, segments: 40, center: [0,0, standoffH/2]}))
        let pin = translate([p[0], p[1], wall + standoffH], cylinder({height: 4, radius: 1.55, segments: 32, center: [0,0, 2]}))
        let split = translate([p[0], p[1], wall + standoffH + 2.5], cuboid({size: [4, 0.8, 3]}))
        return subtract(union(post, pin), split)
    })

    return center({axes: [true, true, false]}, union(baseFinal, ...standoffs))
  }

  const createLid = () => {
    let lidBase = makeBox(outerL, outerW, wall, 0, 0, 0)
    let lip = makeBox(innerL - 0.6, innerW - 0.6, 4.5, wall + 0.3, wall + 0.3, wall)
    let pryNotch = translate([outerL - 5, outerW - 5, wall/2], rotate([0, 0, Math.PI/4], cuboid({size: [10, 2, 5]})))

    const clearance = 0.5
    const lipH = 5.0
    const lipW = 2.0

    const lipOuterL = innerL - (clearance * 2)
    const lipOuterW = innerW - (clearance * 2)

    let lipOuter = makeBox(lipOuterL, lipOuterW, lipH, wall + clearance, wall + clearance, wall)
    let lipInner = makeBox(lipOuterL - (lipW * 2), lipOuterW - (lipW * 2), lipH, wall + clearance + lipW, wall + clearance + lipW, wall)

    let lipRing = subtract(lipOuter, lipInner)

    const lugW = 6.0
    const makeLug = (x, y, w, l) => makeBox(w, l, lipH, x, y, wall)

    let lugs = [
       makeLug(outerL/3, wall + clearance - lidLugSize, lugW, lidLugSize),
       makeLug(2*outerL/3, wall + clearance - lidLugSize, lugW, lidLugSize),
       makeLug(outerL/3, wall + clearance + lipOuterW, lugW, lidLugSize),
       makeLug(2*outerL/3, wall + clearance + lipOuterW, lugW, lidLugSize),
       makeLug(wall + clearance - lidLugSize, outerW/2 - lugW/2, lidLugSize, lugW),
       makeLug(wall + clearance + lipOuterL, outerW/2 - lugW/2, lidLugSize, lugW)
    ]

    let hexVents = []
    for(let x = 15; x < outerL - 15; x += 8.5) {
       for(let y = 15; y < outerW - 15; y += 7.5) {
           let dist = Math.sqrt(Math.pow(outerL/2 - x, 2) + Math.pow(outerW/2 - y, 2))
           let hexSize = Math.max(1.0, 3.8 * (1 - (dist / (outerL/1.8))))
           let xOff = (Math.round(y/7.5) % 2 === 0) ? 0 : 4.25
           hexVents.push(hexCutout(x + xOff, y, wall/2, hexSize, [0, 0, 0]))
       }
    }

    return center({axes: [true, true, false]}, subtract(union(lidBase, lipRing, ...lugs), pryNotch, ...hexVents))
  }

  const createClampRelay = () => {
    const barL = outerW, barW = 10, barH = 8
    let bar = makeBox(barW, barL, barH, 0, 0, 0)
    let hHoles = screwOffsets.map(off => translate([barW/2, outerW/2 + off, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 })));
    let grooves = relayOffsets.map(offset => translate([barW/2, outerW/2 + offset, 0], smoothCyl(barW + 10, relayWireDia/2, 'x')))
    return center({axes: [true, true, false]}, subtract(bar, ...hHoles, ...grooves))
  }

  const createClampDC = () => {
    const barL = 32, barW = 10, barH = 8
    let bar = makeBox(barL, barW, barH, 0, 0, 0)
    let hHoles = [
        translate([4, barW/2, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 })),
        translate([barL - 4, barW/2, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 }))
    ]
    let grooves = [
        translate([barL/2 - 3, barW/2, 0], smoothCyl(barW+10, dcWireDia/2, 'y')),
        translate([barL/2 + 3, barW/2, 0], smoothCyl(barW+10, dcWireDia/2, 'y'))
    ]
    return center({axes: [true, true, false]}, subtract(bar, ...hHoles, ...grooves))
  }

  if (renderPart === 'Base') return createBase()
  if (renderPart === 'Lid') return createLid()
  if (renderPart === 'Clamp_Relay') return createClampRelay()
  if (renderPart === 'Clamp_DC') return createClampDC()

  return [
    createBase(),
    translate([0, outerW + 35, 0], createLid()),
    translate([-outerL/2 - 25, 0, 0], createClampRelay()),
    translate([outerL/2 + 25, 0, 0], createClampRelay()),
    translate([0, -outerW/2 - 35, 0], createClampDC())
  ]
}

module.exports = { main, getParameterDefinitions }
