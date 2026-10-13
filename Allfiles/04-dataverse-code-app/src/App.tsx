import { RequestForm } from './components/RequestForm';
import { FulfilmentContact } from './components/FulfilmentContact';
import './App.css';

function App() {
  return (
    <div className="app-container">
      <header className="app-header">
        <h1>Equipment request</h1>
        <p>Request a replacement for a device you currently hold.</p>
      </header>
      <main className="app-main">
        <RequestForm />
        <FulfilmentContact />
      </main>
    </div>
  );
}

export default App;
