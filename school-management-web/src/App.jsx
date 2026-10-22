import SessionNotice from "./components/SessionNotice";
import BrowserPushLifecycle from "./components/BrowserPushLifecycle";
import AppRoutes from "./routes/AppRoutes";
import SosAlerts from "./components/SosAlerts";

function App() {
    return <><SessionNotice /><BrowserPushLifecycle /><SosAlerts /><AppRoutes /></>;
}

export default App;
