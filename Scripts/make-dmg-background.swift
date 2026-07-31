#!/usr/bin/env swift
//
// Draws the backdrop for the installer window.
//
// Run once and commit the result; the release script only reads the PNG:
//   swift Scripts/make-dmg-background.swift assets/dmg-background.png
//
// The geometry mirrors the window Scripts/make-dmg.sh asks Finder for. The two
// icon centres below have to match the fractions in that script, or the arrow
// ends up pointing at nothing.

import AppKit
import CoreText

let width = 660
let height = 400

// Same fractions the layout uses: quarter and three-quarter width, slightly
// above centre so the icon labels have room.
let appX = CGFloat(width) * 0.26
let applicationsX = CGFloat(width) * 0.74
// Finder measures from the top, Core Graphics from the bottom.
let iconCentreY = CGFloat(height) * (1 - 0.46)

let output = CommandLine.arguments.count > 1
    ? CommandLine.arguments[1]
    : "assets/dmg-background.png"

guard let context = CGContext(
    data: nil,
    width: width,
    height: height,
    bitsPerComponent: 8,
    bytesPerRow: 0,
    space: CGColorSpace(name: CGColorSpace.sRGB)!,
    bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
) else {
    fatalError("Could not create the drawing context")
}

func color(_ hex: UInt32, _ alpha: CGFloat = 1) -> CGColor {
    CGColor(
        srgbRed: CGFloat((hex >> 16) & 0xFF) / 255,
        green: CGFloat((hex >> 8) & 0xFF) / 255,
        blue: CGFloat(hex & 0xFF) / 255,
        alpha: alpha
    )
}

// Backdrop: a light lavender wash, so the app icon's white rounded square still
// reads as the foreground object rather than disappearing into the page.
let gradient = CGGradient(
    colorsSpace: CGColorSpace(name: CGColorSpace.sRGB)!,
    colors: [color(0xFAF8FF), color(0xEDE6FB)] as CFArray,
    locations: [0, 1]
)!
context.drawLinearGradient(
    gradient,
    start: CGPoint(x: 0, y: height),
    end: CGPoint(x: 0, y: 0),
    options: []
)

// A warm bloom in the lower right, picking up the sunset orange the app icon
// uses, so the backdrop belongs to the same product as the icon.
let bloom = CGGradient(
    colorsSpace: CGColorSpace(name: CGColorSpace.sRGB)!,
    colors: [color(0xFF7A45, 0.16), color(0xFF7A45, 0)] as CFArray,
    locations: [0, 1]
)!
context.drawRadialGradient(
    bloom,
    startCenter: CGPoint(x: CGFloat(width) * 0.82, y: 40),
    startRadius: 0,
    endCenter: CGPoint(x: CGFloat(width) * 0.82, y: 40),
    endRadius: 320,
    options: []
)

func draw(_ text: String, size: CGFloat, weight: NSFont.Weight, hex: UInt32, alpha: CGFloat, y: CGFloat) {
    let font = NSFont.systemFont(ofSize: size, weight: weight)
    let attributed = NSAttributedString(string: text, attributes: [
        .font: font,
        .foregroundColor: NSColor(cgColor: color(hex, alpha))!,
        .kern: size * 0.01
    ])
    let line = CTLineCreateWithAttributedString(attributed)
    let bounds = CTLineGetBoundsWithOptions(line, .useOpticalBounds)
    context.textPosition = CGPoint(x: (CGFloat(width) - bounds.width) / 2, y: y)
    CTLineDraw(line, context)
}

draw("Wallpache", size: 25, weight: .semibold, hex: 0x2B2247, alpha: 1, y: CGFloat(height) - 66)
draw("Drag the app onto Applications to install", size: 12.5, weight: .regular, hex: 0x2B2247, alpha: 0.55, y: CGFloat(height) - 90)

// The arrow between the two icons. Drawn as a shaft plus a chevron rather than
// a glyph, so it needs no font to be present at the right weight.
let arrowY = iconCentreY
let shaftFrom = appX + 78
let shaftTo = applicationsX - 78

context.setStrokeColor(color(0x7C5CD6, 0.42))
context.setLineWidth(3)
context.setLineCap(.round)
context.setLineJoin(.round)

context.move(to: CGPoint(x: shaftFrom, y: arrowY))
context.addLine(to: CGPoint(x: shaftTo - 9, y: arrowY))
context.strokePath()

context.move(to: CGPoint(x: shaftTo - 17, y: arrowY + 9))
context.addLine(to: CGPoint(x: shaftTo, y: arrowY))
context.addLine(to: CGPoint(x: shaftTo - 17, y: arrowY - 9))
context.strokePath()

guard let image = context.makeImage() else { fatalError("Could not render") }
let bitmap = NSBitmapImageRep(cgImage: image)
guard let png = bitmap.representation(using: .png, properties: [:]) else {
    fatalError("Could not encode PNG")
}

try png.write(to: URL(fileURLWithPath: output))
print("Wrote \(output) at \(width)x\(height)")
