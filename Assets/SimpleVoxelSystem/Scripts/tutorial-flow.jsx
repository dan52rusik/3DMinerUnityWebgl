import { useState } from "react";

const PC_STEPS = [
  {
    id: "pc1",
    icon: "🖥️",
    label: "Карточка управления",
    desc: "Показывается в верхней части экрана:\nWASD — движение\nПробел — прыжок\nЛКМ — копать",
    condition: "Автоматически при первом запуске на ПК",
    dismiss: "Нажал клавишу или кликнул мышью → исчезает",
    color: "#3b6fd4",
  },
  {
    id: "pc2",
    icon: "✅",
    label: "Готово",
    desc: "PlayerPrefs сохраняет флаг. При следующем запуске обучение не появится.",
    condition: "Через 1.5 сек после первого ввода или через 10 сек",
    dismiss: "",
    color: "#2ea043",
  },
];

const MOB_STEPS = [
  {
    id: "m1",
    icon: "🕹️",
    label: "Затемнение + джойстик",
    desc: "Экран темнеет (α 72%). Золотая рамка подсвечивает джойстик. Стрелка ▼ пульсирует.\n\n\"Это джойстик движения. Подвигай его, чтобы идти вперёд.\"",
    condition: "Первый запуск на мобильном",
    dismiss: "Игрок двигает джойстик 0.25 сек →",
    color: "#d4821b",
  },
  {
    id: "m2",
    icon: "🎮",
    label: "Обзор кнопок",
    desc: "Затемнение снижается (α 62%). Карточка с описанием:\nMINE — копать\nJUMP — прыжок\nACT — взаимодействие\nRUN — бег\nMINIONS — меню миньонов",
    condition: "После шага 1",
    dismiss: "Тап/клик в любом месте →",
    color: "#d4821b",
  },
  {
    id: "m3",
    icon: "🏝️",
    label: "Создай остров",
    desc: "Затемнение (α 55%). Рамка на кнопке Create Island.\n\"Нажми кнопку Create Island. До этого все остальные действия заблокированы.\"\n\n🔒 Движение, копание и шопы — заблокированы!",
    condition: "После тапа на шаге 2",
    dismiss: "WellGen.IsIslandGenerated == true →",
    color: "#c0392b",
    blocked: true,
  },
  {
    id: "m4",
    icon: "⛏️",
    label: "Исследуй остров",
    desc: "Затемнение убрано. Рамка на кнопке \"To Lobby\".\n\n\"Вот и твой остров, мой дорогой шахтер! Исследуй его. Когда насладишься видами — возвращайся обратно в лобби.\"",
    condition: "Игрок появился на острове",
    dismiss: "WellGen.IsInLobbyMode == true →",
    color: "#1a7f64",
  },
  {
    id: "m5",
    icon: "🛒",
    label: "Купи шахту",
    desc: "Карточка в верхней части. Анимированный луч от игрока к триггеру магазина шахт.\n\n\"Для начала приобрети свою первую шахту. Луч указывает на магазин шахт.\"",
    condition: "Игрок вернулся в лобби",
    dismiss: "MineMarket.IsPlacementMode == true →",
    color: "#1a7f64",
  },
  {
    id: "m6",
    icon: "📍",
    label: "Размести шахту",
    desc: "Рамка на кнопке \"To Island\".\n\n\"Отлично! Возвращайся на свой остров и размести купленную шахту.\"",
    condition: "Шахта куплена",
    dismiss: "Mine placed + not in lobby →",
    color: "#1a7f64",
  },
  {
    id: "m7",
    icon: "🏆",
    label: "Обучение завершено",
    desc: "PlayerPrefs сохраняет флаг. UI скрывается. Больше не появится.",
    condition: "Шахта размещена",
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
                БЛОКИРОВКА ВВОДА
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
              <span style={{ color: "#5b8dee" }}>▶ Условие начала: </span>
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
              <span style={{ color: "#56d99b" }}>→ Переход: </span>
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
            🗺️ OnboardingTutorial — Карта шагов
          </h1>
          <p style={{ color: "#6b7280", fontSize: 13, margin: "4px 0 0" }}>
            Нажми на любой шаг, чтобы увидеть детали
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
              {t === "pc" ? "🖥️  ПК" : "📱  Мобильный"}
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
            {steps.length} шагов
          </span>
          {tab === "mobile" && (
            <span style={{ color: "#c0392b", fontWeight: 600 }}>
              🔒 Шаг 3 блокирует весь ввод игрока
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
            Как работает блокировка ввода
          </div>
          <div>
            <span style={{ color: "#e74c3c" }}>PlayerPickaxe</span> — проверяет{" "}
            <code style={{ color: "#56d99b" }}>OnboardingTutorial.IsGameplayInputBlocked</code> в{" "}
            <code>Update()</code> и <code>TryMineGridTarget()</code>
          </div>
          <div>
            <span style={{ color: "#e74c3c" }}>PlayerCharacterController</span> — та же проверка
            перед движением и прыжком
          </div>
          <div>
            <span style={{ color: "#e74c3c" }}>ShopZone</span> — вызывает{" "}
            <code style={{ color: "#56d99b" }}>OnboardingTutorial.IsShopInteractionBlocked()</code>
          </div>
          <div style={{ marginTop: 8, color: "#5b8dee" }}>
            Флаг сбрасывается автоматически при переходе из шага MobCreateIsland.
          </div>
        </div>
      </div>
    </div>
  );
}
