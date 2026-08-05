import type { ComponentType, SVGProps } from "react";

import {
  BatteryIcon,
  CursorIcon,
  DisplaysIcon,
  FolderIcon,
  FrameIcon,
  ShieldIcon,
} from "@/components/ui/icons";

type Icon = ComponentType<SVGProps<SVGSVGElement>>;

export type Feature = {
  title: string;
  body: string;
  icon: Icon;
  /** Gradient pair for the card's icon chip and hover bloom. */
  from: string;
  to: string;
};

export const features: Feature[] = [
  {
    title: "Every display, its own way",
    body: "Each screen keeps its own video, scaling mode, playback speed and mute setting. One loop everywhere, or a different one per monitor.",
    icon: DisplaysIcon,
    from: "#FF7A45",
    to: "#FFB347",
  },
  {
    title: "Never costs you battery",
    body: "Playback stops in Low Power Mode, under thermal pressure, on a locked screen and while the machine sleeps. Every trigger is a toggle you own.",
    icon: BatteryIcon,
    from: "#37D67A",
    to: "#4CC9F0",
  },
  {
    title: "Invisible to your clicks",
    body: "The wallpaper sits below your icons and passes every click straight through to the desktop. It never keeps your display awake or steals focus.",
    icon: CursorIcon,
    from: "#FF4E8E",
    to: "#A855F7",
  },
  {
    title: "Your files stay yours",
    body: "Imports are copied into Wallpache's own storage, so moving or deleting the original later can't break a running wallpaper. Nothing is uploaded.",
    icon: FolderIcon,
    from: "#7C5CD6",
    to: "#4CC9F0",
  },
  {
    title: "Four ways to frame it",
    body: "Fill crops the overflow, Fit letterboxes the whole frame, Stretch ignores aspect ratio, Center pins native pixels. Preview before you commit.",
    icon: FrameIcon,
    from: "#FFB347",
    to: "#FF4E8E",
  },
  {
    title: "Survives the real world",
    body: "Sleep, wake, unplugged monitors, decoder hiccups — playback rebuilds itself instead of leaving a black rectangle. Optional launch at login.",
    icon: ShieldIcon,
    from: "#4CC9F0",
    to: "#7C5CD6",
  },
];

export type Step = {
  title: string;
  body: string;
};

export const steps: Step[] = [
  {
    title: "Install and open",
    body: "Run the one-liner, or drag it out of the disk image. The library window opens by itself the first time.",
  },
  {
    title: "Drop in a video",
    body: "Drag an .mp4, .mov or .m4v onto the window, or press Import Video… It gets copied into the library.",
  },
  {
    title: "Preview the loop",
    body: "Check how it cuts back to the start before it lands on your desktop, and pick a scaling mode while you're there.",
  },
  {
    title: "Apply it",
    body: "Apply covers every display, or use the Display menu on the tile to set one screen at a time. That's it.",
  },
];
