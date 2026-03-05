import { useState } from "react";

const PC_STEPS = [
  {
    id: "pc1",
    icon: "🖥️",
    label: "Control Card",
    desc: "Displayed at the top of the screen:\nWASD — move\nSpace — jump\nLMB — dig",
    condition: "Automatically on first launch on PC",
    dismiss: "Key pressed or mouse clicked → disappears",
    color: "#3b6fd4",
  },
  {
    id: "pc2",
    icon: "✅",
    label: "Done",
    desc: "PlayerPrefs saves the flag. Tutorial won't appear on next launch.",
    condition: "1.5s after first input or after 10s",
    dismiss: "",
    color: "#2ea043",
  },
];

const MOB_STEPS = [
  {
    id: "m1",
    icon: "🕹️",
    label: "Overlay + Joystick",
    desc: "Screen dims (α 72%). Golden frame highlights the joystick. Arrow ▼ pulses.\n\n\"This is the movement joystick. Move it to walk forward.\"",
    condition: "First launch on mobile",
    dismiss: "Player moves joystick for 0.25s →",
    color: "#d4821b",
  },
  {
    id: "m2",
    icon: "🎮",
    label: "Button Overview",
    desc: "Dimming level reduced (α 62%). Info card:\nMINE — dig\nJUMP — jump\nACT — interact\nRUN — run\nMINIONS — minions menu",
    condition: "After Step 1",
    dismiss: "Tap/click anywhere →",
    color: "#d4821b",
  },
  {
    id: "m3",
    icon: "🏝️",
    label: "Create Island",
    desc: "Dimming (α 55%). Frame on Create Island button.\n\"Click the Create Island button. All other actions are blocked until then.\"\n\n🔒 Movement, digging, and shops are blocked!",
    condition: "After tap on Step 2",
    dismiss: "WellGen.IsIslandGenerated == true →",
    color: "#c0392b",
    blocked: true,
  },
  {
    id: "m4",
    icon: "⛏️",
    label: "Explore Island",
    desc: "Dimming removed. Frame on \"To Lobby\" button.\n\n\"Here is your island, my dear miner! Explore it. When you've enjoyed the views, head back to the lobby.\"",
    condition: "Player spawned on island",
    dismiss: "WellGen.IsInLobbyMode == true →",
    color: "#1a7f64",
  },
  {
    id: "m5",
    icon: "🛒",
    label: "Buy Mine",
    desc: "Card at the top. Animated line from player to the Mine Shop trigger.\n\n\"To start, purchase your first mine. The line points to the Mine Shop.\"",
    condition: "Player returned to lobby",
    dismiss: "MineMarket.IsPlacementMode == true →",
    color: "#1a7f64",
  },
  {
    id: "m6",
    icon: "📍",
    label: "Place Mine",
    desc: "Frame on \"To Island\" button.\n\n\"Great! Go back to your island and place the purchased mine.\"",
    condition: "Mine purchased",
    dismiss: "Mine placed + not in lobby →",
    color: "#1a7f64",
  },
  {
    id: "m7",
    icon: "🏆",
    label: "Tutorial Completed",
    desc: "PlayerPrefs saves the flag. UI hidden. Won't appear again.",
    condition: "Mine placed",
    dismiss: "",
    color: "#2ea043",
  },
];

function StepCard({ step, index, total, isActive, onClick }) {
  return (
    <div
      onClick={onClick}
      style={{
        cursor: "pointer",
        borderRadius: 12,
        border: `2px solid ${isActive ? step.color : "#2a2d3a"}`,
        background: isActive ? `${step.color}18` : "#161820",
        padding: "14px 16px",
        transition: "all 0.2s",
        position: "relative",
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
        <span style={{ fontSize: 24 }}>{step.icon}</span>
        <div style={{ flex: 1 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span
              style={{
                fontSize: 11,
                fontWeight: 700,
                color: step.color,
                background: `${step.color}22`,
                borderRadius: 4,
                padding: "1px 7px",
              }}
            >
              {index + 1}/{total}
            </span>
            <span style={{ color: "#e0e4f0", fontWeight: 700, fontSize: 14 }}>
              {step.label}
            </span>
            {step.blocked && (
              <span
                style={{
                  fontSize: 10,
                  background: "#c0392b22",
                  color: "#e74c3c",
                  borderRadius: 4,
                  padding: "1px 7px",
                  fontWeight: 700,
                }}
              >
                INPUT LOCKED
              </span>
            )}
          </div>
          {!isActive && (
            <div style={{ color: "#6b7280", fontSize: 12, marginTop: 2 }}>
              {step.condition}
            </div>
          )}
        </div>
      </div>

      {isActive && (
        <div style={{ marginTop: 12 }}>
          <pre
            style={{
              color: "#c0c8e0",
              fontSize: 13,
              whiteSpace: "pre-wrap",
              fontFamily: "inherit",
              margin: 0,
              lineHeight: 1.55,
            }}
          >
            {step.desc}
          </pre>
          {step.condition && (
            <div
              style={{
                marginTop: 10,
                padding: "6px 10px",
                borderRadius: 6,
                background: "#1a1d28",
                fontSize: 12,
                color: "#8892aa",
              }}
            >
              <span style={{ color: "#5b8dee" }}>▶ Start condition: </span>
              {step.condition}
            </div>
          )}
          {step.dismiss && (
            <div
              style={{
                marginTop: 6,
                padding: "6px 10px",
                borderRadius: 6,
                background: "#1a1d28",
                fontSize: 12,
                color: "#8892aa",
              }}
            >
              <span style={{ color: "#56d99b" }}>→ Transition: </span>
              {step.dismiss}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default function App() {
  const [tab, setTab] = useState("mobile");
  const [activePC, setActivePC] = useState(null);
  const [activeMob, setActiveMob] = useState(null);

  const steps = tab === "pc" ? PC_STEPS : MOB_STEPS;
  const active = tab === "pc" ? activePC : activeMob;
  const setActive = tab === "pc" ? setActivePC : setActiveMob;

  return (
    <div
      style={{
        minHeight: "100vh",
        background: "#0d0f17",
        color: "#e0e4f0",
        fontFamily: "'Segoe UI', system-ui, sans-serif",
        padding: "24px 16px",
      }}
    >
      <div style={{ maxWidth: 680, margin: "0 auto" }}>
        {/* Header */}
        <div style={{ marginBottom: 24 }}>
          <h1
            style={{
              fontSize: 22,
              fontWeight: 800,
              margin: 0,
              color: "#fff",
              letterSpacing: 0.5,
            }}
          >
            🗺️ OnboardingTutorial — Step Map
          </h1>
          <p style={{ color: "#6b7280", fontSize: 13, margin: "4px 0 0" }}>
            Click any step to see details
          </p>
        </div>

        {/* Platform tabs */}
        <div
          style={{
            display: "flex",
            gap: 8,
            marginBottom: 20,
            background: "#161820",
            borderRadius: 10,
            padding: 4,
          }}
        >
          {["pc", "mobile"].map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              style={{
                flex: 1,
                padding: "8px 0",
                borderRadius: 8,
                border: "none",
                cursor: "pointer",
                fontWeight: 700,
                fontSize: 14,
                background: tab === t ? (t === "pc" ? "#3b6fd4" : "#d4821b") : "transparent",
                color: tab === t ? "#fff" : "#6b7280",
                transition: "all 0.15s",
              }}
            >
              {t === "pc" ? "🖥️  PC" : "📱  Mobile"}
            </button>
          ))}
        </div>

        {/* Step count badge */}
        <div
          style={{
            marginBottom: 14,
            fontSize: 12,
            color: "#6b7280",
            display: "flex",
            alignItems: "center",
            gap: 8,
          }}
        >
          <span
            style={{
              background: "#1e2030",
              borderRadius: 6,
              padding: "3px 10px",
              color: "#9aa3b8",
            }}
          >
            {steps.length} steps
          </span>
          {tab === "mobile" && (
            <span style={{ color: "#c0392b", fontWeight: 600 }}>
              🔒 Step 3 blocks all player input
            </span>
          )}
        </div>

        {/* Steps list */}
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {steps.map((s, i) => (
            <StepCard
              key={s.id}
              step={s}
              index={i}
              total={steps.length}
              isActive={active === s.id}
              onClick={() => setActive(active === s.id ? null : s.id)}
            />
          ))}
        </div>

        {/* Legend */}
        <div
          style={{
            marginTop: 28,
            padding: 14,
            borderRadius: 10,
            background: "#161820",
            border: "1px solid #2a2d3a",
            fontSize: 12,
            color: "#6b7280",
            lineHeight: 1.7,
          }}
        >
          <div style={{ fontWeight: 700, color: "#9aa3b8", marginBottom: 4 }}>
            How Input Blocking Works
          </div>
          <div>
            <span style={{ color: "#e74c3c" }}>PlayerPickaxe</span> — checks{" "}
            <code style={{ color: "#56d99b" }}>OnboardingTutorial.IsGameplayInputBlocked</code> in{" "}
            <code>Update()</code> and <code>TryMineGridTarget()</code>
          </div>
          <div>
            <span style={{ color: "#e74c3c" }}>PlayerCharacterController</span> — same check
            before movement and jumping
          </div>
          <div>
            <span style={{ color: "#e74c3c" }}>ShopZone</span> — calls{" "}
            <code style={{ color: "#56d99b" }}>OnboardingTutorial.IsShopInteractionBlocked()</code>
          </div>
          <div style={{ marginTop: 8, color: "#5b8dee" }}>
            Flag is reset automatically when transitioning from MobCreateIsland step.
          </div>
        </div>
      </div>
    </div>
  );
}
