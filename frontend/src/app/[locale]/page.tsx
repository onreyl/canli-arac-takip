"use client";
import dynamic from "next/dynamic";

const LiveMapScreen = dynamic(() => import("@/features/live-map/components/LiveMapScreen"));

export default function Page() {
  return <LiveMapScreen />;
}
