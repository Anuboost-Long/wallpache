import AVFoundation
import Testing

@testable import Wallpache

struct ScalingModeTests {
    @Test(arguments: [
        (ScalingMode.fill, AVLayerVideoGravity.resizeAspectFill),
        (.fit, .resizeAspect),
        (.stretch, .resize),
        (.center, .resizeAspect)
    ])
    func modesMapToTheExpectedVideoGravity(mode: ScalingMode, gravity: AVLayerVideoGravity) {
        #expect(mode.videoGravity == gravity)
    }

    @Test func onlyCenterSizesTheLayerToTheNaturalVideoSize() {
        for mode in ScalingMode.allCases {
            #expect(mode.usesNaturalSize == (mode == .center))
        }
    }

    @Test func rawValuesAreStableForPersistence() {
        #expect(ScalingMode.allCases.map(\.rawValue) == ["fill", "fit", "stretch", "center"])
    }
}
