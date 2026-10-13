import './App.css';
import { ProfileCard } from './components/ProfileCard';

function App() {
  return (
    <div className="app">
      <h1>Equipment profile viewer</h1>
      <p>
        This code app shows the signed-in user's profile through a Power Platform
        connection.
      </p>
      <ProfileCard />
    </div>
  );
}

export default App;
