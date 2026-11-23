// 영상 → 장면 사진 (ffmpeg 없이 macOS AVFoundation). swift frames.swift <영상> <폴더> <초당 장수>
import AVFoundation
import AppKit
let url = URL(fileURLWithPath: CommandLine.arguments[1])
let out = CommandLine.arguments[2]
let fps = Double(CommandLine.arguments.count > 3 ? CommandLine.arguments[3] : "4") ?? 4
let asset = AVURLAsset(url: url)
let dur = CMTimeGetSeconds(asset.duration)
let gen = AVAssetImageGenerator(asset: asset)
gen.appliesPreferredTrackTransform = true
gen.requestedTimeToleranceBefore = .zero; gen.requestedTimeToleranceAfter = .zero
gen.maximumSize = CGSize(width: 1400, height: 1400)
let n = max(1, Int(dur * fps))
for i in 0..<n {
  let t = CMTime(seconds: Double(i) / fps, preferredTimescale: 600)
  if let cg = try? gen.copyCGImage(at: t, actualTime: nil) {
    let data = NSBitmapImageRep(cgImage: cg).representation(using: .png, properties: [:])!
    try! data.write(to: URL(fileURLWithPath: String(format: "%@/f%04d.png", out, i)))
  }
}
print(String(format: "길이 %.1f초 · %d장", dur, n))
