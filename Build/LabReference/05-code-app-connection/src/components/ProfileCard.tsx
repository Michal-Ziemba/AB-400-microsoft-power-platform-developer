import { useEffect, useState } from 'react';
import './ProfileCard.css';
import { Office365UsersService } from '../generated/services/Office365UsersService';

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
        const result = await Office365UsersService.MyProfile_V2(
          'displayName,jobTitle,department',
        );
        setProfile({
          displayName: result.data.displayName ?? '',
          jobTitle: result.data.jobTitle ?? '',
          department: result.data.department ?? '',
        });
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
