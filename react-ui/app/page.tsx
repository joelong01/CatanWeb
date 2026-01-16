/**
 * Home page - entry point for the Catan application.
 * Provides navigation to create new games, load saved games, etc.
 */
export default function Home(): React.ReactElement {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center p-8">
      <h1 className="text-4xl font-bold mb-4 text-text-primary">Catan</h1>
      <p className="text-text-muted mb-8">React/Next.js port in progress</p>

      <nav className="flex flex-col gap-4 w-full max-w-xs">
        <a
          href="/new-game"
          className="px-6 py-3 bg-accent-primary hover:bg-accent-hover rounded-lg text-center text-white transition-colors"
        >
          New Game
        </a>
        <a
          href="/load-game"
          className="px-6 py-3 bg-game-bg-panel hover:bg-game-bg-secondary rounded-lg text-center text-text-primary transition-colors border border-overlay-light"
        >
          Load Game
        </a>
      </nav>

      {/* Theme color preview */}
      <section className="mt-12 w-full max-w-2xl">
        <h2 className="text-xl font-semibold mb-4 text-text-secondary">Player Colors</h2>
        <div className="grid grid-cols-3 gap-3">
          <div className="p-4 rounded-lg bg-player-white-primary text-player-white-foreground text-center">
            White
          </div>
          <div className="p-4 rounded-lg bg-player-blue-primary text-player-blue-foreground text-center">
            Blue
          </div>
          <div className="p-4 rounded-lg bg-player-orange-primary text-player-orange-foreground text-center">
            Orange
          </div>
          <div className="p-4 rounded-lg bg-player-red-primary text-player-red-foreground text-center">
            Red
          </div>
          <div className="p-4 rounded-lg bg-player-green-primary text-player-green-foreground text-center">
            Green
          </div>
          <div className="p-4 rounded-lg bg-player-brown-primary text-player-brown-foreground text-center">
            Brown
          </div>
        </div>

        <h2 className="text-xl font-semibold mb-4 mt-8 text-text-secondary">Resource Colors</h2>
        <div className="grid grid-cols-3 gap-3">
          <div className="p-4 rounded-lg bg-resource-wheat text-black text-center">Wheat</div>
          <div className="p-4 rounded-lg bg-resource-wood text-white text-center">Wood</div>
          <div className="p-4 rounded-lg bg-resource-brick text-white text-center">Brick</div>
          <div className="p-4 rounded-lg bg-resource-sheep text-black text-center">Sheep</div>
          <div className="p-4 rounded-lg bg-resource-ore text-white text-center">Ore</div>
          <div className="p-4 rounded-lg bg-resource-desert text-black text-center">Desert</div>
        </div>
      </section>
    </main>
  );
}
