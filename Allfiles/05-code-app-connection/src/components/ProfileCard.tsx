import { useEffect, useState } from 'react';
import './ProfileCard.css';

// Import the generated Office 365 Users service here


interface Profile {
  displayName: string;
  jobTitle: string;
  department: string;
}

export function ProfileCard() {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    async function loadProfile() {
      try {
        // Call the Office 365 Users connection's MyProfile_V2 operation here
        // and replace the line below with the real response
        setError('Profile data will appear here once the connection is wired up.');
      } catch {
        setProfile(null);
        setError("Unable to load the signed-in user's profile.");
      } finally {
        setIsLoading(false);
      }
    }

    loadProfile();
  }, []);

  if (isLoading) {
    return <p className="profile-status">Loading your profile...</p>;
  }

  if (error || !profile) {
    return <p className="profile-status">{error}</p>;
  }

  return (
    <div className="profile-card">
      <h2>{profile.displayName}</h2>
      <p className="profile-job-title">{profile.jobTitle}</p>
      <p className="profile-department">{profile.department}</p>
    </div>
  );
}
